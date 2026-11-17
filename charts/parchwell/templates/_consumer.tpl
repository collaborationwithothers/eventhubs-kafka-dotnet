{{- /* Scenarios that run consumer member Jobs and the consumer verifier modes. */ -}}
{{- define "parchwell.consumerScenarios" -}}C01 C03 C10{{- end -}}
{{- define "parchwell.isConsumerScenario" -}}
{{- if has .Values.scenario_id (splitList " " (include "parchwell.consumerScenarios" .)) -}}true{{- end -}}
{{- end -}}

{{- /* One consumer group member (subject or witness). The worker binds ConsumerProcessing__* with unknown names rejected. */ -}}
{{- define "parchwell.consumerJob" -}}
{{- $r := .Values.runtime -}}
{{- $role := trimPrefix "consumer-" $r.mode -}}
{{- $job := include "parchwell.jobName" . -}}
{{- $account := index $r.service_accounts "enrichment-consumer" -}}
{{- if not (include "parchwell.isConsumerScenario" .) }}{{ fail "consumer member modes require scenario C01, C03 or C10" }}{{ end -}}
{{- if not (has .Values.configuration_id (list "C0" "C1")) }}{{ fail "consumer member modes require configuration C0 or C1" }}{{ end -}}
{{- if ne $r.phase .Values.configuration_id }}{{ fail "phase must equal configuration for consumer members" }}{{ end -}}
{{- if not $r.enrichment_image }}{{ fail "consumer members require enrichment_image" }}{{ end -}}
{{- if not $account }}{{ fail "consumer members require the enrichment-consumer service account" }}{{ end -}}
{{- if and $r.loadgen_job_name (ne $r.loadgen_job_name (printf "%s-%s-r%d-loadgen" $r.run_id (lower $r.phase) (int $r.repeat))) }}{{ fail "loadgen_job_name must equal the derived producer Job name" }}{{ end -}}
{{- if or (contains " " $r.blob.account_url) (contains "\t" $r.blob.account_url) }}{{ fail "blob account URL must be an HTTPS account-root URI" }}{{ end -}}
{{- if ne $r.eventhubs.bootstrap_servers (printf "%s.servicebus.windows.net:9093" $r.eventhubs.namespace_name) }}{{ fail "bootstrap_servers must match namespace_name" }}{{ end -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ $job | quote }}
  namespace: parchwell
  labels:
    {{- include "parchwell.labels" . | nindent 4 }}
    parchwell.io/role: "enrichment-consumer"
spec:
  completions: 1
  parallelism: 1
  backoffLimit: 0
  podReplacementPolicy: Failed
  activeDeadlineSeconds: {{ int $r.bounds.execution.consumer_job_seconds }}
  template:
    metadata:
      labels:
        {{- include "parchwell.labels" . | nindent 8 }}
        parchwell.io/role: "enrichment-consumer"
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: {{ $account.name | quote }}
      automountServiceAccountToken: false
      restartPolicy: Never
      {{- /* The subject kills itself with signal 9; a pod's PID 1 ignores that, so no worker may be PID 1. */}}
      shareProcessNamespace: true
      terminationGracePeriodSeconds: {{ $r.bounds.execution.termination_grace_seconds }}
      nodeSelector:
        workload: "workload"
      securityContext:
        runAsUser: 1654
        runAsGroup: 1654
        fsGroup: 1654
        runAsNonRoot: true
        seccompProfile:
          type: RuntimeDefault
      containers:
        - name: "enrichment-consumer"
          image: {{ $r.enrichment_image | quote }}
          imagePullPolicy: IfNotPresent
          args: ["--consumer-processing"]
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: true
            capabilities:
              drop: [ALL]
          resources:
            requests:
              cpu: 500m
              memory: 1Gi
            limits:
              cpu: "1500m"
              memory: 4Gi
          env:
            - name: PARCHWELL_SOURCE_COMMIT
              value: {{ $r.source_commit | quote }}
            - name: PARCHWELL_JOB_NAME
              value: {{ $job | quote }}
            - name: PARCHWELL_POD_NAME
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: PARCHWELL_POD_UID
              valueFrom: {fieldRef: {fieldPath: metadata.uid}}
            - name: PARCHWELL_NODE_NAME
              valueFrom: {fieldRef: {fieldPath: spec.nodeName}}
            {{- range $key, $value := dict "RunId" $r.run_id "ScenarioId" $.Values.scenario_id "Phase" $r.phase "RepeatNumber" (printf "%d" (int $r.repeat)) "Role" $role "JobName" $job "SourceCommit" $r.source_commit "NamespaceName" $r.eventhubs.namespace_name "BlobEndpoint" $r.blob.account_url }}
            - name: {{ printf "ConsumerProcessing__%s" $key | quote }}
              value: {{ $value | quote }}
            {{- end }}
            {{- range $key, $path := dict "PodName" "metadata.name" "PodUid" "metadata.uid" "NodeName" "spec.nodeName" }}
            - name: {{ printf "ConsumerProcessing__%s" $key | quote }}
              valueFrom: {fieldRef: {fieldPath: {{ $path }}}}
            {{- end }}
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
{{- end -}}
