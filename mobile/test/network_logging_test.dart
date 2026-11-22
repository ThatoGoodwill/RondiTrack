import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/network/client.dart';

void main() {
  test('private network fields are absent from request log line', () {
    final options = RequestOptions(path: '/api/v1/me/home?email=secret@example.org',
      method: 'GET',
      headers: {'Authorization':'Bearer TOP_SECRET', 'Cookie': 'refresh_token=123'},
      data: {'name': 'Lerato', 'amount': 'R250'});
    final line = safeRequestLine(options, 403, 'trace-123');
    expect(line, contains('403'));
    expect(line.contains('email='), isFalse);
    expect(safeRequestLine(RequestOptions(path: '/api/v1/stokvels/Lerato-Surname', method: 'GET'), 200, null).contains('Lerato'), isFalse);
    for (final hidden in ['TOP_SECRET', 'refresh_token', 'secret@example.org',
      'Lerato', 'R250']) {
      expect(line.contains(hidden), isFalse);
    }
  });
}
