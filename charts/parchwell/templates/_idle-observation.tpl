{{- define "parchwell.idleObservationJob" -}}
{{- $r := .Values.runtime -}}
{{- $pairs := list "A02/C0" "A02/C1" "A03/C0" "A03/C1" "A03/C2" "A04/C0" "A04/C1" -}}
{{- if or (ne $r.phase .Values.configuration_id) (not (has (printf "%s/%s" .Values.scenario_id .Values.configuration_id) $pairs)) -}}{{ fail "idle-observation item/configuration/phase mismatch" }}{{- end -}}
{{- $job := include "parchwell.jobName" . -}}
{{- if or (contains " " $r.blob.account_url) (contains "\t" $r.blob.account_url) }}{{ fail "blob account URL must be an HTTPS account-root URI" }}{{ end -}}
{{- range $ip := list $r.eventhubs_ip $r.blob_ip -}}
{{- $parts := splitList "." $ip -}}
{{- if ne (len $parts) 4 }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}
{{- range $part := $parts -}}{{- if or (not (regexMatch `^[0-9]{1,3}$` $part)) (lt (int $part) 0) (gt (int $part) 255) }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}{{- end -}}
{{- end -}}
{{- if ne $r.eventhubs.bootstrap_servers (printf "%s.servicebus.windows.net:9093" $r.eventhubs.namespace_name) }}{{ fail "bootstrap_servers must match namespace_name" }}{{ end -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ $job | quote }}
  namespace: parchwell
  labels:
    {{- include "parchwell.labels" . | nindent 4 }}
    parchwell.io/role: "loadgen"
spec:
  completions: 1
  parallelism: 1
  backoffLimit: 0
  podReplacementPolicy: Failed
  activeDeadlineSeconds: {{ int $r.bounds.idle_observation.execution.job_seconds }}
  template:
    metadata:
      labels:
        {{- include "parchwell.labels" . | nindent 8 }}
        parchwell.io/role: "loadgen"
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: {{ $r.service_accounts.loadgen.name | quote }}
      automountServiceAccountToken: false
      restartPolicy: Never
      terminationGracePeriodSeconds: {{ $r.bounds.idle_observation.execution.termination_grace_seconds }}
      nodeSelector:
        workload: "loadgen"
      tolerations:
        - key: workload
          operator: Equal
          value: loadgen
          effect: NoSchedule
      securityContext:
        runAsUser: 1654
        runAsGroup: 1654
        fsGroup: 1654
        runAsNonRoot: true
        seccompProfile:
          type: RuntimeDefault
      containers:
        - name: loadgen
          image: {{ $r.loadgen_image | quote }}
          imagePullPolicy: IfNotPresent
          args: ["--idle-observation"]
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
              value: "Parchwell.LoadGen"
            - name: Telemetry__ServiceInstanceId
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: Telemetry__ScenarioId
              value: {{ $.Values.scenario_id | quote }}
            - name: Telemetry__RunId
              value: {{ $r.run_id | quote }}
            {{- $fields := dict "ScenarioId" .Values.scenario_id "ConfigurationId" .Values.configuration_id "RunId" $r.run_id "RunKind" $r.run_kind "SourceCommit" $r.source_commit "JobName" $job "NamespaceName" $r.eventhubs.namespace_name "BlobEndpoint" $r.blob.account_url "BlobContainer" $r.blob.containers.ledger "ScenarioRoot" "/app" -}}
            {{- range $key, $value := $r.bounds.idle_observation.capture -}}{{- $_ := set $fields $key $value -}}{{- end -}}
            {{- range $key, $value := $r.bounds.scenario_parsing -}}{{- $_ := set $fields (printf "ScenarioLoadOptions__%s" $key) $value -}}{{- end }}
            {{- range $key, $value := $fields }}
            - name: {{ printf "IdleObservation__%s" $key }}
              value: {{ include "parchwell.envValue" $value | quote }}
            {{- end }}
            - name: IdleObservation__PodName
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: IdleObservation__PodUid
              valueFrom: {fieldRef: {fieldPath: metadata.uid}}
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
{{- define "parchwell.idleObservationJudgeJob" -}}
{{- $r := .Values.runtime -}}
{{- if or (ne .Values.scenario_id "A04") (ne .Values.configuration_id "C1") (ne $r.phase "C1") -}}{{ fail "idle-observation judge scenario/configuration/phase mismatch" }}{{- end -}}
{{- $job := include "parchwell.jobName" . -}}
{{- if or (contains " " $r.blob.account_url) (contains "\t" $r.blob.account_url) }}{{ fail "blob account URL must be an HTTPS account-root URI" }}{{ end -}}
{{- range $ip := list $r.eventhubs_ip $r.blob_ip -}}
{{- $parts := splitList "." $ip -}}
{{- if ne (len $parts) 4 }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}
{{- range $part := $parts -}}{{- if or (not (regexMatch `^[0-9]{1,3}$` $part)) (lt (int $part) 0) (gt (int $part) 255) }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}{{- end -}}
{{- end -}}
{{- if ne $r.eventhubs.bootstrap_servers (printf "%s.servicebus.windows.net:9093" $r.eventhubs.namespace_name) }}{{ fail "bootstrap_servers must match namespace_name" }}{{ end -}}
apiVersion: batch/v1
kind: Job
metadata:
  name: {{ $job | quote }}
  namespace: parchwell
  labels:
    {{- include "parchwell.labels" . | nindent 4 }}
    parchwell.io/role: "verifier"
spec:
  completions: 1
  parallelism: 1
  backoffLimit: 0
  activeDeadlineSeconds: {{ int $r.bounds.idle_observation.execution.judge_job_seconds }}
  template:
    metadata:
      labels:
        {{- include "parchwell.labels" . | nindent 8 }}
        parchwell.io/role: "verifier"
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: {{ $r.service_accounts.verifier.name | quote }}
      automountServiceAccountToken: false
      restartPolicy: Never
      terminationGracePeriodSeconds: {{ $r.bounds.idle_observation.execution.termination_grace_seconds }}
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
        - name: verifier
          image: {{ $r.verifier_image | quote }}
          imagePullPolicy: IfNotPresent
          args: ["--idle-observation-judge"]
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
              value: "Parchwell.Verifier"
            - name: Telemetry__ServiceInstanceId
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: Telemetry__ScenarioId
              value: "idle-observation"
            - name: Telemetry__RunId
              value: {{ $r.run_id | quote }}
            {{- $fields := dict "RunKind" $r.run_kind "RunId" $r.run_id "SourceCommit" $r.source_commit "JobName" $job "NamespaceName" $r.eventhubs.namespace_name "ServiceAccount" $r.service_accounts.verifier.name "WorkloadIdentityClientId" $r.service_accounts.verifier.client_id "ScenarioRoot" "/app" "BlobServiceUri" $r.blob.account_url "LedgerContainer" $r.blob.containers.ledger "VerifierContainer" "verifier" "LoadGenImage" $r.loadgen_image "VerifierImage" $r.verifier_image "CapturePods" $r.capture_pods "Items" $r.judge_items -}}
            {{- range $key, $value := $r.bounds.idle_observation.judge -}}{{- $_ := set $fields (printf "Limits__%s" $key) $value -}}{{- end -}}
            {{- range $key, $value := $r.bounds.scenario_parsing -}}{{- $_ := set $fields (printf "ScenarioLoadOptions__%s" $key) $value -}}{{- end }}
            {{- range $key, $value := $fields }}
            - name: {{ printf "IdleObservationJudge__%s" $key }}
              value: {{ include "parchwell.envValue" $value | quote }}
            {{- end }}
            - name: IdleObservationJudge__PodName
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: IdleObservationJudge__PodUid
              valueFrom: {fieldRef: {fieldPath: metadata.uid}}
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
