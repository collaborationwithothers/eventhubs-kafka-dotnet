{{- define "parchwell.labels" -}}
app.kubernetes.io/name: parchwell
app.kubernetes.io/instance: {{ .Values.runtime.run_id | quote }}
{{- end -}}
{{- define "parchwell.role" -}}{{- if eq .Values.runtime.mode "loadgen" -}}loadgen{{- else -}}verifier{{- end -}}{{- end -}}
{{- define "parchwell.jobName" -}}
{{- $r := .Values.runtime -}}{{- $phase := lower $r.phase -}}
{{- if eq $r.mode "judge" -}}{{ printf "%s-%s-judge" $r.run_id $phase }}{{- else -}}{{ printf "%s-%s-r%d-%s" $r.run_id $phase (int $r.repeat) $r.mode }}{{- end -}}
{{- end -}}
{{- define "parchwell.repeatPrefix" -}}{{ printf "%s/%s/r%d/" .Values.runtime.run_id .Values.runtime.phase (int .Values.runtime.repeat) }}{{- end -}}
{{- define "parchwell.phasePrefix" -}}{{ printf "%s/%s/" .Values.runtime.run_id .Values.runtime.phase }}{{- end -}}
