{{- define "parchwell.probeV2Spec" -}}
{{- $r := .Values.runtime -}}
{{- $item := .Values.scenario_id -}}
{{- $configuration := .Values.configuration_id -}}
{{- $stage := $r.stage -}}
{{- $index := int (trimPrefix "C" $configuration) -}}
{{- $roles := dict "action" "loadgen" "read" "probe" "judge" "verifier" -}}
{{- $own := list "action" "read" -}}
{{- $sources := list -}}
{{- if eq $item "B08" -}}
{{- if not (has $configuration (list "C0" "C1")) }}{{ fail "probe-v2 configuration mismatch" }}{{ end -}}
{{- else if eq $item "E01" -}}
{{- if not (has $configuration (list "C0" "C1")) }}{{ fail "probe-v2 configuration mismatch" }}{{ end -}}
{{- $roles = dict "seed" "enrichment-consumer" "seedread" "probe" "action" "probe" "read" "probe" "judge" "verifier" -}}
{{- $own = list "seed" "seedread" "action" "read" -}}
{{- else if eq $item "F06" -}}
{{- if not (has $configuration (list "C0" "C1" "C2" "C3" "C4" "C5" "C6" "C7" "C8" "C9" "C10" "C11")) }}{{ fail "probe-v2 configuration mismatch" }}{{ end -}}
{{- if lt $index 6 -}}
{{- $_ := set $roles "action" (ternary "probe" "loadgen" (has $configuration (list "C1" "C3" "C5"))) -}}
{{- else -}}
{{- $own = list "action" -}}
{{- $roles = dict "action" (ternary "probe" "enrichment-consumer" (lt $index 9)) "judge" "verifier" -}}
{{- if lt $index 9 -}}
{{- $_ := set $roles "seed" (ternary "loadgen" "enrichment-consumer" (eq $index 6)) -}}
{{- $own = list "seed" "action" -}}
{{- else -}}
{{- $sourceConfiguration := printf "c%d" (sub $index 3) -}}
{{- $sources = list (printf "%s-f06-%s-seed" $r.run_id $sourceConfiguration) (printf "%s-f06-%s-action" $r.run_id $sourceConfiguration) -}}
{{- end -}}
{{- end -}}
{{- else -}}{{ fail "probe-v2 item mismatch" }}{{- end -}}
{{- $role := get $roles $stage -}}
{{- if or (eq $role "") (ne $r.action_role $role) (ne $r.phase $configuration) (ne $r.mode (ternary "single-action-v2-judge" "single-action-v2" (eq $stage "judge"))) -}}
{{- fail "probe-v2 mode/item/configuration/stage/role mismatch" -}}
{{- end -}}
{{- $dependencies := list -}}
{{- $prefix := printf "%s-%s-%s-" $r.run_id (lower $item) (lower $configuration) -}}
{{- if eq $stage "judge" -}}
{{- range $dependency := $own -}}{{ $dependencies = append $dependencies (printf "%s%s" $prefix $dependency) }}{{- end -}}
{{- $dependencies = concat $dependencies $sources -}}
{{- else if eq $item "E01" -}}
{{- if has $stage (list "seedread" "action" "read") }}{{ $dependencies = append $dependencies (printf "%sseed" $prefix) }}{{ end -}}
{{- if has $stage (list "action" "read") }}{{ $dependencies = append $dependencies (printf "%sseedread" $prefix) }}{{ end -}}
{{- if eq $stage "read" }}{{ $dependencies = append $dependencies (printf "%saction" $prefix) }}{{ end -}}
{{- else if eq $stage "read" -}}
{{- $dependencies = append $dependencies (printf "%saction" $prefix) -}}
{{- else if and (eq $item "F06") (eq $stage "action") (ge $index 6) -}}
{{- if lt $index 9 }}{{ $dependencies = append $dependencies (printf "%sseed" $prefix) }}{{ else }}{{ $dependencies = $sources }}{{ end -}}
{{- end -}}
{{- $producer := or (eq $stage "seed") (and (eq $stage "action") (or (ne $item "F06") (lt $index 6))) -}}
{{- dict "role" $role "producer" $producer "dependencies" $dependencies | toJson -}}
{{- end -}}

{{- define "parchwell.probeV2Job" -}}
{{- $r := .Values.runtime -}}
{{- $spec := include "parchwell.probeV2Spec" . | fromJson -}}
{{- $role := $spec.role -}}
{{- $judge := eq $r.stage "judge" -}}
{{- $account := index $r.service_accounts $role -}}
{{- $worker := ternary "verifier" "loadgen" $judge -}}
{{- $job := printf "%s-%s-%s-%s" $r.run_id (lower .Values.scenario_id) (lower .Values.configuration_id) $r.stage -}}
{{- if gt (len $job) 63 }}{{ fail "probe-v2 Job name too long" }}{{ end -}}
{{- if or (contains " " $r.blob.account_url) (contains "\t" $r.blob.account_url) }}{{ fail "blob account URL must be an HTTPS account-root URI" }}{{ end -}}
{{- range $ip := list $r.eventhubs_ip $r.blob_ip -}}
{{- $parts := splitList "." $ip -}}
{{- if ne (len $parts) 4 }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}
{{- range $part := $parts -}}{{- if or (not (regexMatch `^[0-9]{1,3}$` $part)) (lt (int $part) 0) (gt (int $part) 255) }}{{ fail "private endpoint IP must be IPv4" }}{{ end -}}{{- end -}}
{{- end -}}
{{- if ne $r.eventhubs.bootstrap_servers (printf "%s.servicebus.windows.net:9093" $r.eventhubs.namespace_name) }}{{ fail "bootstrap_servers must match namespace_name" }}{{ end -}}
{{- $bounds := $r.bounds.probe_v2 -}}
{{- $deadline := ternary $bounds.execution.producer_job_seconds $bounds.execution.reader_job_seconds $spec.producer -}}
{{- if $judge }}{{ $deadline = $bounds.execution.judge_job_seconds }}{{ end -}}
{{- $prefix := ternary "ProbeV2Judge" "ProbeV2" $judge -}}
{{- $fields := dict "ScenarioId" .Values.scenario_id "ConfigurationId" .Values.configuration_id "RunId" $r.run_id "SourceCommit" $r.source_commit "JobName" $job "NamespaceName" $r.eventhubs.namespace_name "ServiceAccount" $account.name "WorkloadIdentityClientId" $account.client_id "ScenarioRoot" "/app" "InputRoot" "/run/probe-inputs" "Stage" $r.stage -}}
{{- range $key,$value := $bounds.capture }}{{ $_ := set $fields (printf "Limits__%s" $key) $value }}{{ end -}}
{{- range $key,$value := $bounds.parsing }}{{ $_ := set $fields (printf "ScenarioLoadOptions__%s" $key) $value }}{{ end -}}
{{- if $judge -}}
{{- $_ := set $fields "RunKind" $r.run_kind -}}
{{- $_ := set $fields "BlobServiceUri" $r.blob.account_url -}}
{{- $_ := set $fields "VerifierContainer" "verifier" -}}
{{- $_ := set $fields "LoadGenImage" $r.loadgen_image -}}
{{- $_ := set $fields "VerifierImage" $r.verifier_image -}}
{{- range $key,$value := $bounds.judge }}{{ $_ := set $fields (printf "JudgeLimits__%s" $key) $value }}{{ end -}}
{{- else -}}
{{- $_ := set $fields "ProcessLifetimeSeconds" (ternary $bounds.execution.producer_process_seconds $bounds.execution.reader_process_seconds $spec.producer) -}}
{{- end -}}
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
  {{- if not $judge }}
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
      terminationGracePeriodSeconds: {{ $bounds.execution.termination_grace_seconds }}
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
          args: [{{ ternary "--image-smoke" (ternary "--single-action-v2-judge" "--single-action-v2" $judge) $r.image_smoke | quote }}]
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
              value: {{ printf "Parchwell.%s" (ternary "Verifier" "LoadGen" $judge) | quote }}
            - name: Telemetry__ServiceInstanceId
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: Telemetry__ScenarioId
              value: {{ .Values.scenario_id | quote }}
            - name: Telemetry__RunId
              value: {{ $r.run_id | quote }}
            {{- range $key,$value := $fields }}
            - name: {{ printf "%s__%s" $prefix $key | quote }}
              value: {{ include "parchwell.envValue" $value | quote }}
            {{- end }}
            - name: {{ printf "%s__PodName" $prefix | quote }}
              valueFrom: {fieldRef: {fieldPath: metadata.name}}
            - name: {{ printf "%s__PodUid" $prefix | quote }}
              valueFrom: {fieldRef: {fieldPath: metadata.uid}}
          volumeMounts:
            - name: work
              mountPath: /work
            - name: tmp
              mountPath: /tmp
            {{- range $index,$dependency := $spec.dependencies }}
            - name: {{ printf "evidence-%d" $index }}
              mountPath: {{ printf "/run/probe-inputs/%s" $dependency | quote }}
              readOnly: true
            {{- end }}
            {{- if $judge }}
            {{- if and (eq $r.run_kind "batch") (eq .Values.scenario_id "F06") }}
            - name: access-check
              mountPath: /run/probe-inputs/access
              readOnly: true
            {{- end }}
            - name: role-scope
              mountPath: /run/probe-inputs/scope
              readOnly: true
            {{- end }}
      volumes:
        - name: work
          emptyDir: {sizeLimit: 8Gi}
        - name: tmp
          emptyDir: {}
        {{- range $index,$dependency := $spec.dependencies }}
        - name: {{ printf "evidence-%d" $index }}
          projected:
            sources:
              {{- range $part := until 4 }}
              - configMap:
                  name: {{ printf "%s-evidence%s" $dependency (ternary "" (printf "-%d" $part) (eq $part 0)) | quote }}
                  optional: true
                  items:
                    {{- if eq $part 0 }}
                    - {key: registration.json, path: registration.json}
                    - {key: pod.json, path: pod.json}
                    {{- end }}
                    - {key: {{ printf "capture.json.part%d" $part }}, path: {{ printf "capture.json.part%d" $part }}}
              {{- end }}
        {{- end }}
        {{- if $judge }}
        {{- if and (eq $r.run_kind "batch") (eq .Values.scenario_id "F06") }}
        - name: access-check
          configMap:
            name: {{ printf "%s-v2-access-check" $r.run_id | quote }}
            optional: true
            items:
              - {key: access-check.json, path: access-check.json}
        {{- end }}
        - name: role-scope
          configMap:
            name: {{ printf "%s-v2-role-scope" $r.run_id | quote }}
            optional: true
            items:
              - {key: role-scope.json, path: role-scope.json}
        {{- end }}
{{- end -}}
