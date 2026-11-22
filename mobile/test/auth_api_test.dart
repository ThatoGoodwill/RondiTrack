import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/auth/data/auth_api.dart';

void main() {
  test('login decodes access token and expiry in UTC without persistence', () async {
    final dio = Dio(BaseOptions(baseUrl: 'https://example.invalid'));
    dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) {
      expect(options.path, '/api/v1/auth/login');
      expect(options.method, 'POST');
      handler.resolve(Response<dynamic>(requestOptions: options,
        statusCode: 200, data: {
          'accessToken': 'test-value',
          'expiresAt': '2026-11-23T10:30:00Z',
        }));
    }));
    final result = await AuthApi(dio).login('test@example.org', 'test-password');
    expect(result.token, 'test-value');
    expect(result.expiresAt.isUtc, isTrue);
  });
}
