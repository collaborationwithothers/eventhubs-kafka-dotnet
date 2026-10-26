{{- define "parchwell.singleActionJob" -}}
{{- include "parchwell.validateSingleAction" . -}}
{{- $r := .Values.runtime -}}
{{- $mode := $r.mode -}}
{{- $role := include "parchwell.role" . -}}
{{- $worker := include "parchwell.worker" . -}}
{{- $job := include "parchwell.jobName" . -}}
{{- $account := index $r.service_accounts $role -}}
{{- if or (contains " " $r.blob.account_url) (contains "\t" $r.blob.account_url) }}{{ fail "blob account URL must be an HTTPS account-root URI" }}{{ end -}}
{{- range $ip := list $r.eventhubs_ip $r.blob_ip -}}
{{- $parts := splitList "." $ip -}}
{{- if ne (len $parts) 4 }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}
{{- range $part := $parts -}}{{- if or (not (regexMatch `^[0-9]{1,3}$` $part)) (lt (int $part) 0) (gt (int $part) 255) }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}{{- end -}}
{{- end -}}
{{- if ne $r.eventhubs.bootstrap_servers (printf "%s.servicebus.windows.net:9093" $r.eventhubs.namespace_name) }}{{ fail "bootstrap_servers must match namespace_name" }}{{ end -}}
{{- $deadline := int $r.bounds.single_action_execution.action_job_seconds -}}
{{- if eq $mode "single-action-judge" }}{{ $deadline = int $r.bounds.single_action_execution.judge_job_seconds }}{{ end }}
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ $job | quote }}
  namespace: parchwell
  labels:
    {{- include "parchwell.labels" . | nindent 4 }}
    parchwell.io/role: {{ $role | quote }}
spec:
  completions: 1
  parallelism: 1
  backoffLimit: 0
  {{- if ne $mode "single-action-judge" }}
  podReplacementPolicy: Failed
  {{- end }}
  activeDeadlineSeconds: {{ $deadline }}
  template:
    metadata:
      labels:
        {{- include "parchwell.labels" . | nindent 8 }}
        parchwell.io/role: {{ $role | quote }}
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: {{ $account.name | quote }}
      automountServiceAccountToken: false
      restartPolicy: Never
      terminationGracePeriodSeconds: {{ $r.bounds.single_action_execution.termination_grace_seconds }}
      nodeSelector:
        workload: {{ ternary "loadgen" "workload" (eq $role "loadgen") | quote }}
      {{- if eq $role "loadgen" }}
      tolerations:
        - key: workload
          operator: Equal
          value: loadgen
          effect: NoSchedule
      {{- end }}
      securityContext:
        runAsUser: 1654
        runAsGroup: 1654
        fsGroup: 1654
        runAsNonRoot: true
        seccompProfile:
          type: RuntimeDefault
      containers:
        - name: {{ $role }}
          image: {{ index $r (printf "%s_image" $worker) | quote }}
          imagePullPolicy: IfNotPresent
          args: ["--image-smoke"]
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: true
            capabilities:
              drop: [ALL]
          resources:
            requests:
              cpu: "1"
              memory: 2Gi
            limits:
              cpu: "3"
              memory: 12Gi
          env:
            - name: PARCHWELL_SOURCE_COMMIT
              value: {{ $r.source_commit | quote }}
            - name: PARCHWELL_JOB_NAME
              value: {{ $job | quote }}
            - name: PARCHWELL_POD_NAME
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: PARCHWELL_POD_UID
              valueFrom: {fieldRef: {fieldPath: metadata.uid}}
            - name: Telemetry__ConnectionString
              value: {{ $r.application_insights_connection_string | quote }}
            - name: Telemetry__ServiceName
              value: {{ ternary "Parchwell.LoadGen" "Parchwell.Verifier" (eq $worker "loadgen") | quote }}
            - name: Telemetry__ServiceInstanceId
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: Telemetry__ScenarioId
              value: {{ $.Values.scenario_id | quote }}
            - name: Telemetry__RunId
              value: {{ $r.run_id | quote }}
          volumeMounts:
            - name: work
              mountPath: /work
            - name: tmp
              mountPath: /tmp
      volumes:
        - name: work
          emptyDir: {sizeLimit: 8Gi}
        - name: tmp
          emptyDir: {}
{{- end }}
