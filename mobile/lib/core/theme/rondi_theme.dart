import 'package:flutter/material.dart';

@immutable
class RondiStatusColors extends ThemeExtension<RondiStatusColors> {
  const RondiStatusColors({required this.recorded, required this.arrears,
    required this.open, required this.closed, required this.paidOut,
    required this.payoutWaiting, required this.payoutNext, required this.payoutPaid,
    required this.unknown});
  final Color recorded, arrears, open, closed, paidOut;
  final Color payoutWaiting, payoutNext, payoutPaid, unknown;

  @override
  RondiStatusColors copyWith({Color? recorded, Color? arrears, Color? open,
    Color? closed, Color? paidOut, Color? payoutWaiting,
    Color? payoutNext, Color? payoutPaid, Color? unknown}) => RondiStatusColors(
      recorded: recorded ?? this.recorded, arrears: arrears ?? this.arrears,
      open: open ?? this.open, closed: closed ?? this.closed,
      paidOut: paidOut ?? this.paidOut,
      payoutWaiting: payoutWaiting ?? this.payoutWaiting,
      payoutNext: payoutNext ?? this.payoutNext,
      payoutPaid: payoutPaid ?? this.paidOut,
      unknown: unknown ?? this.unknown);

  @override
  RondiStatusColors lerp(ThemeExtension<RondiStatusColors>? other, double t) {
    if (other is! RondiStatusColors) return this;
    Color blend(Color a, Color b) => Color.lerp(a, b, t)!;
    return RondiStatusColors(recorded: blend(recorded, other.recorded),
      arrears: blend(arrears, other.arrears), open: blend(open, other.open),
      closed: blend(closed, other.closed), paidOut: blend(paidOut, other.paidOut),
      payoutWaiting: blend(payoutWaiting, other.payoutWaiting),
      payoutNext: blend(payoutNext, other.payoutNext),
      payoutPaid: blend(payoutPaid, other.payoutPaid),
      unknown: blend(unknown, other.unknown));
  }
}

ThemeData rondiTheme(Brightness brightness) {
  final dark = brightness == Brightness.dark;
  final colors = RondiStatusColors(
    recorded: dark ? const Color(0xFF9AD6B4) : const Color(0xFF135C38),
    arrears: dark ? const Color(0xFFFFB4AB) : const Color(0xFF9B1C24),
    open: dark ? const Color(0xFF9AD6B4) : const Color(0xFF135C38),
    closed: dark ? const Color(0xFFFFD68A) : const Color(0xFF785100),
    paidOut: dark ? const Color(0xFF9ABDF5) : const Color(0xFF174C98),
    payoutWaiting: dark ? const Color(0xFFFFD68A) : const Color(0xFF785100),
    payoutNext: dark ? const Color(0xFF9ABDF5) : const Color(0xFF174C98),
    payoutPaid: dark ? const Color(0xFF9AD6B4) : const Color(0xFF135C38),
    unknown: dark ? const Color(0xFFCECAD4) : const Color(0xFF57545E),
  );
  return ThemeData(useMaterial3: true, brightness: brightness,
    colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF087C78),
      brightness: brightness), extensions: [colors]);
}
