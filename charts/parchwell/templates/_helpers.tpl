{{- define "parchwell.labels" -}}
app.kubernetes.io/name: parchwell
app.kubernetes.io/instance: {{ .Values.runtime.run_id | quote }}
{{- end -}}
