import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/money/cents.dart';

void main() {
  test('formats South African amounts with nonbreaking spaces', () {
    expect(const Cents(10).formatZa(), 'R\u00A00,10');
    expect(const Cents(15050).formatZa(), 'R\u00A0150,50');
    expect(const Cents(150000).formatZa(), 'R\u00A01\u00A0500,00');
    expect(const Cents(100000000).formatZa(), 'R\u00A01\u00A0000\u00A0000,00');
    expect(const Cents(-15050).formatZa(), '-R\u00A0150,50');
  });
  test('parse accepted SA rand notations', () {
    for (final text in ['150', 'R150', 'R 150', '150,00']) {
      final result = parseRands(text);
      expect(result, isA<ValidRands>());
      expect((result as ValidRands).cents.value, 15000);
    }
    for (final text in ['1 500,50', 'R 1 500,50', 'R\u00A01\u00A0500,50']) {
      final result = parseRands(text);
      expect((result as ValidRands).cents.value, 150050);
    }
    expect((parseRands('19.99') as ValidRands).cents.value, 1999);
  });
  test('reject ambiguity, rounding, negatives, letters and excessive cents', () {
    for (final text in ['1,500.50', '150,505', '150.', '', 'abc', '-1', '1000000,01']) {
      expect(parseRands(text), isA<InvalidRands>(), reason: text);
    }
  });
}
