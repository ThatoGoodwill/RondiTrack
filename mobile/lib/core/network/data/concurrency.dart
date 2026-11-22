import 'package:dio/dio.dart';

/// An ETag is opaque. Retain the quotes and value exactly as the API sent it.
extension type const ETag(String value) {}

ETag? etagFromResponse(Response<dynamic> response) {
  final raw = response.headers.value('etag');
  return raw == null ? null : ETag(raw);
}

Options withIfMatch(ETag token) => Options(
  headers: {'If-Match': token.value},
);

/// The API's only valid end marker is a null nextPageToken.
String? nextCursor(String? nextPageToken) =>
    nextPageToken == null || nextPageToken.isEmpty ? null : nextPageToken;
