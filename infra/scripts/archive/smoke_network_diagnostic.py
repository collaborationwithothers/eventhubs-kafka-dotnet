#!/usr/bin/env python3
"""Independent bounded connectivity observation, without sending an HTTP request."""
import json
import socket
import ssl
import sys

TLS_REASONS = {'CERTIFICATE_VERIFY_FAILED', 'WRONG_VERSION_NUMBER',
               'TLSV1_ALERT_PROTOCOL_VERSION', 'SSLV3_ALERT_HANDSHAKE_FAILURE',
               'UNEXPECTED_EOF_WHILE_READING', 'NO_CIPHERS_AVAILABLE',
               'CERTIFICATE_REQUIRED', 'UNKNOWN_CA', 'TLSV1_ALERT_UNKNOWN_CA'}


def main():
    data = json.load(sys.stdin)
    result = {'dns': 'not_attempted', 'tcp': 'not_attempted', 'tls': 'not_attempted',
              'errno': None, 'tls_reason': None, 'tls_verify_code': None, 'tls_failure_kind': None}
    try:
        addresses = socket.getaddrinfo(data['host'], 443, type=socket.SOCK_STREAM)
    except OSError as error:
        result['dns'] = 'failure'
        if type(error.errno) is int and -255 <= error.errno <= 255: result['errno'] = error.errno
        addresses = None
    if addresses == []:
        result['dns'] = 'empty'
    elif addresses:
        result['dns'] = 'success'
        family, kind, protocol, _, address = addresses[0]
        try:
            with socket.socket(family, kind, protocol) as sock:
                sock.settimeout(4)
                sock.connect(address)
                result['tcp'] = 'success'
                try:
                    with ssl.create_default_context().wrap_socket(sock, server_hostname=data['host']):
                        result['tls'] = 'success'
                except (OSError, ValueError) as error:
                    result['tls'] = 'failure'
                    result['tls_failure_kind'] = ('certificate_verification' if isinstance(error, ssl.SSLCertVerificationError) else
                                                  'ssl' if isinstance(error, ssl.SSLError) else
                                                  'timeout' if isinstance(error, TimeoutError) else
                                                  'configuration' if isinstance(error, ValueError) else 'os_error')
                    if isinstance(error, ssl.SSLCertVerificationError):
                        code = getattr(error, 'verify_code', None)
                        if type(code) is int and 0 <= code <= 255: result['tls_verify_code'] = code
                    reason = getattr(error, 'reason', None)
                    if isinstance(reason, str) and reason in TLS_REASONS: result['tls_reason'] = reason
        except OSError as error:
            result['tcp'] = 'failure'
            if type(error.errno) is int and 0 <= error.errno <= 255: result['errno'] = error.errno
    print(json.dumps(result, separators=(',', ':')))


if __name__ == '__main__':
    main()
