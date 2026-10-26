{{- define "parchwell.labels" -}}
app.kubernetes.io/name: parchwell
app.kubernetes.io/instance: {{ .Values.runtime.run_id | quote }}
{{- end -}}
{{- define "parchwell.role" -}}
{{- $r := .Values.runtime -}}
{{- if eq $r.mode "single-action" -}}{{ $r.action_role }}
{{- else if eq $r.mode "single-action-seed" -}}enrichment-consumer
{{- else if eq $r.mode "loadgen" -}}loadgen
{{- else -}}verifier{{- end -}}
{{- end -}}
{{- define "parchwell.worker" -}}
{{- if eq .Values.runtime.mode "single-action-seed" -}}enrichment
{{- else if has .Values.runtime.mode (list "single-action" "loadgen") -}}loadgen{{- else -}}verifier{{- end -}}
{{- end -}}
{{- define "parchwell.jobName" -}}
{{- $r := .Values.runtime -}}{{- $phase := lower $r.phase -}}
{{- if eq $r.mode "single-action-seed" -}}{{ printf "%s-f04-seed" $r.run_id }}
{{- else if eq $r.mode "single-action" -}}{{ printf "%s-%s-%s-single" $r.run_id (lower .Values.scenario_id) (lower .Values.configuration_id) }}
{{- else if eq $r.mode "single-action-judge" -}}{{ printf "%s-%s-%s-single-judge" $r.run_id (lower .Values.scenario_id) (lower .Values.configuration_id) }}
{{- else if eq $r.mode "judge" -}}{{ printf "%s-%s-judge" $r.run_id $phase }}{{- else -}}{{ printf "%s-%s-r%d-%s" $r.run_id $phase (int $r.repeat) $r.mode }}{{- end -}}
{{- end -}}
{{- define "parchwell.repeatPrefix" -}}{{ printf "%s/%s/r%d/" .Values.runtime.run_id .Values.runtime.phase (int .Values.runtime.repeat) }}{{- end -}}
{{- define "parchwell.phasePrefix" -}}{{ printf "%s/%s/" .Values.runtime.run_id .Values.runtime.phase }}{{- end -}}
{{- define "parchwell.envValue" -}}{{- if kindIs "float64" . -}}{{ printf "%.0f" . }}{{- else -}}{{ printf "%v" . }}{{- end -}}{{- end -}}
{{- define "parchwell.validateSingleAction" -}}
{{- $r := .Values.runtime -}}
{{- $actions := dict
  "B01" (dict "C0" "loadgen" "C1" "loadgen")
  "B03" (dict "C1" "loadgen" "C2" "loadgen" "C3" "loadgen" "C4" "loadgen")
  "B05" (dict "C1" "loadgen")
  "C08" (dict "C0" "probe" "C1" "probe")
  "C12" (dict "C1" "probe" "C2" "probe")
  "F04" (dict "C0" "loadgen" "C1" "loadgen" "C2" "probe" "C3" "loadgen" "C4" "probe") -}}
{{- $configurations := get $actions .Values.scenario_id | default dict -}}
{{- $expected := get $configurations .Values.configuration_id -}}
{{- if eq $r.mode "single-action-judge" -}}{{ $expected = ternary "verifier" "" (ne $expected "") }}
{{- else if eq $r.mode "single-action-seed" -}}{{ $expected = ternary "enrichment-consumer" "" (and (eq .Values.scenario_id "F04") (eq .Values.configuration_id "C2")) }}{{ end -}}
{{- if or (ne $r.phase .Values.configuration_id) (eq $expected "") (ne $r.action_role $expected) -}}
{{- fail "single-action mode/item/configuration/role mismatch" -}}
{{- end -}}
{{- end -}}

{{- define "parchwell.validateSeedReceipt" -}}
{{- $r := .Values.runtime -}}
{{- $needs := and (not $r.image_smoke) (eq $r.mode "single-action-judge") (eq .Values.scenario_id "F04") -}}
{{- if $needs -}}
{{- if ne ($r.seed_receipt_configmap | default "") (printf "%s-f04-seed-receipt" $r.run_id) -}}{{ fail "live F04 judge requires exact seed_receipt_configmap" }}{{- end -}}
{{- else if $r.seed_receipt_configmap -}}{{ fail "seed_receipt_configmap only permitted for live F04 judge" }}{{- end -}}
{{- end -}}
