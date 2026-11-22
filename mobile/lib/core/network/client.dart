import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../config/app_config.dart';
import 'session.dart';

/// Base URL is always supplied by the selected build flavour.
final appConfigProvider = Provider<AppConfig>((ref) => AppConfig.load());

final dioProvider = Provider<Dio>((ref) {
  final config = ref.watch(appConfigProvider);
  final dio = Dio(BaseOptions(
    baseUrl: config.apiBaseUrl.toString(),
    connectTimeout: const Duration(seconds: 5),
    sendTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 20),
    responseType: ResponseType.json,
  ));
  dio.interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) {
      final token = ref.read(accessSessionProvider)?.token;
      if (token != null) options.headers['Authorization'] = 'Bearer $token';
      options.extra['startTime'] = DateTime.now();
      handler.next(options);
    },
    onResponse: (response, handler) {
      logSafeRequest(response.requestOptions, response.statusCode,
          response.headers.value('X-Correlation-ID'));
      handler.next(response);
    },
    onError: (error, handler) {
      logSafeRequest(error.requestOptions, error.response?.statusCode,
          error.response?.headers.value('X-Correlation-ID'));
      handler.next(error);
    },
  ));
  ref.onDispose(dio.close);
  return dio;
});

/// Privacy-preserving request summary: never logs a URL query, headers or body.
String safeRequestLine(RequestOptions options, int? status, String? reference) {
  final started = options.extra['startTime'];
  final elapsed = started is DateTime
      ? DateTime.now().difference(started).inMilliseconds
      : 0;
  final method = options.method.replaceAll(RegExp('[^A-Z]'), '');
  // Route path segments can be patient or membership identifiers: redact them.
  const safeSegments = {
    'api', 'v1', 'auth', 'login', 'me', 'home', 'stokvels',
    'cycles', 'contributions', 'payout', 'order', 'activity',
    'members', 'payouts', 'refresh', 'logout',
  };
  final path = options.uri.pathSegments.map((part) {
    if (part.isEmpty) return '';
    return safeSegments.contains(part) ? part : ':value';
  }).join('/');
  final safeRef = reference == null ? '-' :
      reference.replaceAll(RegExp(r'[^a-zA-Z0-9-]'), '');
  return '$method /$path ${status ?? 0} ${elapsed}ms ref=$safeRef';
}

void logSafeRequest(RequestOptions options, int? status, String? reference) {
  // No network payload, person, token, cookie, query, or request headers.
  // ignore: avoid_print
  print(safeRequestLine(options, status, reference));
}
