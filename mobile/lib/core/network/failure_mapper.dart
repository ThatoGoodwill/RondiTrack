import 'package:dio/dio.dart';
import 'failure.dart';

/// The only HTTP-to-domain error translation used by repositories.
Failure mapFailure(Object error) {
  if (error is Failure) return error;
  if (error is! DioException) return const UnexpectedFailure();

  final response = error.response;
  if (response == null) return const OfflineFailure();
  final status = response.statusCode;
  final body = response.data;
  final problem = body is Map ? Map<String, dynamic>.from(body) : <String, dynamic>{};
  final reference = problem['correlationId']?.toString() ??
      response.headers.value('X-Correlation-ID') ??
      response.headers.value('X-Request-ID');
  final detail = problem['detail'] is String ? problem['detail'] as String : '';
  final rawErrors = problem['errors'];
  final fields = <String, List<String>>{};
  if (rawErrors is Map) {
    for (final entry in rawErrors.entries) {
      final value = entry.value;
      fields[entry.key.toString()] = value is List
          ? value.map((item) => item.toString()).toList()
          : [value.toString()];
    }
  }
  return switch (status ?? 0) {
    400 => ValidationFailure(detail, fields: fields, reference: reference),
    401 => UnauthorisedFailure(reference: reference),
    403 => ForbiddenFailure(reference: reference),
    404 => NotFoundFailure(reference: reference),
    409 || 412 => ConflictFailure(reference: reference),
    422 => RuleFailure(detail, reference: reference),
    429 => RateLimitedFailure(
      int.tryParse(response.headers.value('retry-after') ?? ''),
      reference: reference),
    >= 500 => ServerFailure(reference: reference),
    _ => UnexpectedFailure(reference: reference),
  };
}
