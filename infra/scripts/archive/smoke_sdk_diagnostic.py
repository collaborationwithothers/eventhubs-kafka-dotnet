#!/usr/bin/env python3
"""Read-only independent SDK observation; stdin contains an ephemeral access token."""
import json
import sys

# No exception messages, URLs, paths, headers, credentials or response bodies leave
# this process. The caller validates every output field again before publication.
TYPES = {'ClientAuthenticationError', 'ServiceRequestError', 'ServiceResponseError',
         'HttpResponseError', 'ResourceNotFoundError', 'ResourceExistsError',
         'ResourceModifiedError', 'SSLError', 'ConnectionError', 'ConnectTimeout',
         'ReadTimeout', 'TimeoutError', 'NameResolutionError', 'gaierror',
         'PermissionError', 'FileNotFoundError', 'OSError', 'ValueError', 'TypeError',
         'ImportError', 'ModuleNotFoundError'}
CODES = {'BlobNotFound', 'ResourceNotFound', 'BlobAlreadyExists', 'ConditionNotMet',
         'AuthenticationFailed', 'AuthorizationFailure', 'AuthorizationPermissionMismatch',
         'AuthorizationResourceTypeMismatch', 'AuthorizationServiceMismatch', 'ContainerNotFound',
         'AccountIsDisabled', 'InvalidAuthenticationInfo', 'InvalidHeaderValue',
         'InvalidQueryParameterValue', 'InvalidUri', 'InvalidResourceName', 'ServerBusy',
         'OperationTimedOut', 'InternalError', 'ServiceUnavailable'}


def main():
    result = {'outcome': 'failure', 'exception_types': [], 'errno': None,
              'http_status': None, 'service_code': None, 'request_id': None}
    try:
        data = json.load(sys.stdin)
        from azure.core.credentials import AccessToken
        from azure.storage.blob import BlobServiceClient
        class Credential:
            def get_token(self, *_args, **_kwargs):
                return AccessToken(data['token'], data['expires_on'])
        client = BlobServiceClient('https://' + data['host'], credential=Credential(),
                                   retry_total=0, connection_timeout=5, read_timeout=5)
        with client:
            client.get_container_client(data['container']).get_container_properties()
        result['outcome'] = 'success'
    except Exception as error:
        seen = set()
        pending = [error]
        while pending and len(seen) < 16:
            error = pending.pop(0)
            if not isinstance(error, BaseException) or id(error) in seen: continue
            seen.add(id(error))
            name = type(error).__name__
            if name in TYPES and name not in result['exception_types']:
                result['exception_types'].append(name)
            number = getattr(error, 'errno', None)
            if type(number) is int and 0 <= number <= 255:
                result['errno'] = number
            status = getattr(error, 'status_code', None)
            if type(status) is int and 100 <= status <= 599:
                result['http_status'] = status
            code = getattr(error, 'error_code', None)
            if hasattr(code, 'value'): code = code.value
            if isinstance(code, str) and code in CODES: result['service_code'] = code
            response = getattr(error, 'response', None)
            if response is not None:
                import re
                request = response.headers.get('x-ms-request-id', '')
                if isinstance(request, str) and re.fullmatch(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', request):
                    result['request_id'] = request.lower()
            pending.extend((getattr(error, 'inner_exception', None), error.__cause__, error.__context__))
    print(json.dumps(result, separators=(',', ':')))


if __name__ == '__main__':
    main()
