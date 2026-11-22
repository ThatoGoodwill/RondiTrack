import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/config/app_config.dart';
import 'package:ronditrack_mobile/core/network/client.dart';
import 'package:ronditrack_mobile/core/network/data/concurrency.dart';
import 'package:ronditrack_mobile/core/network/session.dart';

void main() {
  test('bearer is added to every request and a 403 does not clear the token', () async {
    final container = ProviderContainer(overrides: [
      appConfigProvider.overrideWithValue(AppConfig(
        flavor: 'prod', apiBaseUrl: Uri.parse('https://example.invalid'))),
    ]);
    addTearDown(container.dispose);
    container.read(accessSessionProvider.notifier).start(
      'TEST_ONLY_TOKEN', DateTime.utc(2026, 12, 1));
    final dio = container.read(dioProvider);
    dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) {
      expect(options.headers['Authorization'], 'Bearer TEST_ONLY_TOKEN');
      handler.reject(DioException(
        requestOptions: options,
        response: Response<dynamic>(requestOptions: options,
          statusCode: 403, data: {'detail': 'Forbidden'})));
    }));
    await expectLater(dio.get<dynamic>('/api/v1/me/home'), throwsA(isA<DioException>()));
    expect(container.read(accessSessionProvider)?.token, 'TEST_ONLY_TOKEN');
  });

  test('ETag is returned unchanged in If-Match', () {
    final request = RequestOptions(path: '/api/v1/payouts/example');
    final response = Response<dynamic>(requestOptions: request,
      headers: Headers.fromMap({'etag': ['"123"']}));
    final original = etagFromResponse(response);
    expect(original?.value, '"123"');
    expect(withIfMatch(original!).headers?['If-Match'], '"123"');
  });

  test('null and empty page token both mean the end', () {
    expect(nextCursor(null), isNull);
    expect(nextCursor(''), isNull);
    expect(nextCursor('opaque-token'), 'opaque-token');
  });
}
