{{- define "parchwell.singleActionJob" -}}
{{- include "parchwell.validateSingleAction" . -}}
{{- include "parchwell.validateSeedReceipt" . -}}
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
{{- if eq $mode "single-action-seed" }}{{ $deadline = int $r.bounds.single_action_execution.seed_job_seconds }}{{ end }}
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
          {{- if $r.image_smoke }}
          args: ["--image-smoke"]
          {{- else if eq $mode "single-action" }}
          args: ["--single-action"]
          {{- else if eq $mode "single-action-seed" }}
          args: ["--single-action-seed"]
          {{- end }}
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
            {{- if ne $worker "enrichment" }}
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
            {{- end }}
            {{- if not $r.image_smoke }}
            {{- $prefix := "Verifier" -}}
            {{- $fields := dict "Mode" "single-action-judge" "ScenarioId" $.Values.scenario_id "ConfigurationId" $.Values.configuration_id "RunId" $r.run_id "VerifierCommit" $r.source_commit "JobName" $job "BlobServiceUri" $r.blob.account_url "VerifierContainer" $r.blob.containers.verifier "NamespaceName" $r.eventhubs.namespace_name "ScenarioRoot" "/app" "LoadGenImage" $r.loadgen_image "VerifierImage" $r.verifier_image "EnrichmentImage" $r.enrichment_image -}}
            {{- $actions := dict "B01" (dict "C0" "loadgen" "C1" "loadgen") "B03" (dict "C1" "loadgen" "C2" "loadgen" "C3" "loadgen" "C4" "loadgen") "B05" (dict "C1" "loadgen") "C08" (dict "C0" "probe" "C1" "probe") "C12" (dict "C1" "probe" "C2" "probe") "F04" (dict "C0" "loadgen" "C1" "loadgen" "C2" "probe" "C3" "loadgen" "C4" "probe") -}}
            {{- $captureRole := index (index $actions $.Values.scenario_id) $.Values.configuration_id -}}
            {{- $captureContainer := ternary $r.blob.containers.ledger $r.blob.containers.single_action (eq $captureRole "loadgen") -}}
            {{- if eq $mode "single-action" -}}
            {{- $prefix = "SingleAction" -}}
            {{- $fields = dict "ScenarioId" $.Values.scenario_id "ConfigurationId" $.Values.configuration_id "RunId" $r.run_id "SourceCommit" $r.source_commit "JobName" $job "NamespaceName" $r.eventhubs.namespace_name "BlobEndpoint" $r.blob.account_url "BlobContainer" $captureContainer "ScenarioRoot" "/app" -}}
            {{- range $key, $value := $r.bounds.single_action_capture -}}{{- $_ := set $fields $key $value -}}{{- end -}}
            {{- else if eq $mode "single-action-seed" -}}
            {{- $prefix = "SeedF04" -}}
            {{- $fields = dict "RunId" $r.run_id "SourceCommit" $r.source_commit "JobName" $job "NamespaceName" $r.eventhubs.namespace_name "ActionWindowMs" 330000 "ProcessLifetimeSeconds" 390 "CleanupSeconds" 10 "HostCleanupSeconds" 10 "MaxReceiptBytes" 65536 -}}
            {{- else -}}
            {{- $_ := set $fields "CaptureContainer" $captureContainer -}}
            {{- $_ := set $fields "CapturePrefix" (printf "%s/single-action/%s/%s/" $r.run_id $.Values.scenario_id $.Values.configuration_id) -}}
            {{- if eq $.Values.scenario_id "F04" -}}
            {{- $_ := set $fields "SeedReceiptPath" "/run/seed/receipt.json" -}}
            {{- $_ := set $fields "SeedPodIdentityPath" "/run/seed/pod.json" -}}
            {{- end -}}
            {{- range $key, $value := $r.bounds.single_action_judge -}}{{- $_ := set $fields (printf "SingleActionLimits__%s" $key) $value -}}{{- end -}}
            {{- end -}}
            {{- if ne $mode "single-action-seed" -}}
            {{- range $key, $value := $r.bounds.scenario_parsing -}}{{- $_ := set $fields (printf "ScenarioLoadOptions__%s" $key) $value -}}{{- end -}}
            {{- end }}
            {{- range $key, $value := $fields }}
            - name: {{ printf "%s__%s" $prefix $key }}
              value: {{ include "parchwell.envValue" $value | quote }}
            {{- end }}
            - name: {{ printf "%s__PodName" $prefix }}
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: {{ printf "%s__PodUid" $prefix }}
              valueFrom: {fieldRef: {fieldPath: metadata.uid}}
            {{- end }}
          volumeMounts:
            - name: work
              mountPath: /work
            - name: tmp
              mountPath: /tmp
            {{- if and (not $r.image_smoke) (eq $mode "single-action-judge") (eq $.Values.scenario_id "F04") }}
            - name: seed-receipt
              mountPath: /run/seed
              readOnly: true
            {{- end }}
      volumes:
        - name: work
          emptyDir: {sizeLimit: 8Gi}
        - name: tmp
          emptyDir: {}
        {{- if and (not $r.image_smoke) (eq $mode "single-action-judge") (eq $.Values.scenario_id "F04") }}
        - name: seed-receipt
          configMap:
            name: {{ $r.seed_receipt_configmap | quote }}
            optional: true
            items:
              - {key: receipt.json, path: receipt.json}
              - {key: pod.json, path: pod.json}
        {{- end }}
{{- end }}
