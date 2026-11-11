// A type, not a double: all arithmetic stays in integer cents.
/// Documented RondiTrack type behavior.
extension type const Cents(int value) {
  /// Documented RondiTrack String behavior.
  String formatZa() {
    final negative = value < 0;
    final absolute = value.abs();
    final whole = (absolute ~/ 100).toString();
    final groups = whole.replaceAllMapped(
      RegExp(r'\B(?=(\d{3})+(?!\d))'),
      (match) => '\u00A0',
    );
    final fraction = (absolute % 100).toString().padLeft(2, '0');
    return (negative ? '-R\u00A0' : 'R\u00A0') + groups + ',' + fraction;
  }
}

/// Documented RondiTrack class behavior.
sealed class RandParseResult {
  const new();
}

/// Documented RondiTrack class behavior.
final class ValidRands extends RandParseResult {
  /// Documented RondiTrack new behavior.
  const new(this.cents);
  /// Documented RondiTrack Cents behavior.
  final Cents cents;
}

/// Documented RondiTrack class behavior.
final class InvalidRands extends RandParseResult {
  /// Documented RondiTrack new behavior.
  const new(this.reason);
  /// Documented RondiTrack String behavior.
  final String reason;
}

/// One optional decimal separator, dot or comma, never thousands commas.
RandParseResult parseRands(String input) {
  final text = input.trim().replaceAll('\u00A0', ' ');
  final number = text.replaceFirst(RegExp(r'^R\s?'), '').trim();
  if (!RegExp(r'^(?:0|[1-9]\d*|[1-9]\d{0,2}(?: \d{3})+)(?:[,.]\d{1,2})?$').hasMatch(number)) {
    return const InvalidRands('Use R 1 500,50 and at most two decimal places.');
  }
  final clean = number.replaceAll(' ', '');
  final parts = clean.split(RegExp(r'[,.]'));
  final rands = int.tryParse(parts.first);
  if (rands == null) return const InvalidRands('Invalid rands');
  final fraction = parts.length == 1 ? '00' : parts.last.padRight(2, '0');
  final decimal = int.tryParse(fraction);
  if (decimal == null) return const InvalidRands('Invalid cents');
  // The API's Contribution.MaxAmount is R 1,000,000.00.
  if (rands > 1000000 || (rands == 1000000 && decimal > 0)) {
    return const InvalidRands('Above the contribution limit');
  }
  return ValidRands(Cents(rands * 100 + decimal));
}
