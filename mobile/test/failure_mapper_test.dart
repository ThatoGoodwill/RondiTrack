import 'dart:async';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/network/failure.dart';
import 'package:ronditrack_mobile/core/network/failure_mapper.dart';

DioException responseError(int status, {Object? data, Map<String, List<String>> headers = const {}}) {
  final request = RequestOptions(path: '/private');
  return DioException(requestOptions: request,
    response: Response<dynamic>(requestOptions: request, statusCode: status,
      data: data, headers: Headers.fromMap(headers)));
}

void main() {
  test('every relevant response is one typed failure', () {
    expect(mapFailure(responseError(400, data: {'detail':'Invalid', 'errors': {'Name':['Required']}})),
      isA<ValidationFailure>());
    expect(mapFailure(responseError(401)), isA<UnauthorisedFailure>());
    expect(mapFailure(responseError(403)), isA<ForbiddenFailure>());
    expect(mapFailure(responseError(404)), isA<NotFoundFailure>());
    expect(mapFailure(responseError(409)), isA<ConflictFailure>());
    expect(mapFailure(responseError(412)), isA<ConflictFailure>());
    expect(mapFailure(responseError(422)), isA<RuleFailure>());
    expect(mapFailure(responseError(500, data: '<html>unavailable</html>')),
      isA<ServerFailure>());
    expect(mapFailure(responseError(502, data: '<html>gateway</html>')),
      isA<ServerFailure>());
    expect(mapFailure(DioException(requestOptions: RequestOptions(path: '/')),
      ), isA<OfflineFailure>());
  });
  test('404 inside Dart record ParallelWaitError stays not found', () async {
    try {
      await (
        Future<int>.error(responseError(404)),
        Future<int>.value(1),
      ).wait;
      fail('Expected the record wait to throw');
    } on ParallelWaitError catch (error) {
      expect(mapFailure(error), isA<NotFoundFailure>());
    }
  });
  test('rate-limit uses server Retry-After, not a fixed duration', () {
    final failure = mapFailure(responseError(429, headers: {'retry-after': ['37']}));
    expect(failure, isA<RateLimitedFailure>());
    expect((failure as RateLimitedFailure).waitSeconds, 37);
  });
  test('server failures never leak raw body', () {
    final failure = mapFailure(responseError(500, data: {
      'detail': 'DB PASSWORD hidden',
      'correlationId': 'trace-123',
    }));
    expect(failure.message.contains('PASSWORD'), isFalse);
    expect(failure.reference, 'trace-123');
    expect(failure.canRetry, isFalse);
  });
}
