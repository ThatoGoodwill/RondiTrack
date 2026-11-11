import 'package:freezed_annotation/freezed_annotation.dart';
import 'package:ronditrack_mobile/lib/core/money/cents.dart';
import 'package:ronditrack_mobile/lib/features/stokvel/domain/statuses.dart';

part 'models.freezed.dart';

// A membership owns the role. The user and access token have no role.
@freezed
abstract class Membership with _$Membership {
  /// Documented RondiTrack factory behavior.
  const factory Membership({
    /// Documented RondiTrack required behavior.
    required String stokvelId,
    required StokvelRole role,
  }) = _Membership;
}

@freezed
abstract class CurrentCycle with _$CurrentCycle {
  /// Documented RondiTrack factory behavior.
  const factory CurrentCycle({
    /// Documented RondiTrack required behavior.
    required String id,
    required String label,
    required CycleStatus status,
    required Cents paidCents,
    required Cents owedCents,
    String? endsOnDate, // The API DateOnly stays YYYY-MM-DD, not a UTC instant.
  }) = _CurrentCycle;
}

@freezed
abstract class HomeEntry with _$HomeEntry {
  /// Documented RondiTrack factory behavior.
  const factory HomeEntry({
    /// Documented RondiTrack required behavior.
    required Membership membership,
    required String name,
    required Cents arrearsCents,
    CurrentCycle? currentCycle,
    DateTime? nextPayoutDueUtc,
  }) = _HomeEntry;
}

@freezed
abstract class ContributionEntry with _$ContributionEntry {
  /// Documented RondiTrack factory behavior.
  const factory ContributionEntry({
    /// Documented RondiTrack required behavior.
    required String id,
    required String userId,
    required Cents amountCents,
    required DateTime recordedAtUtc,
    required ContributionStatus status,
  }) = _ContributionEntry;
}

@freezed
abstract class StokvelDetails with _$StokvelDetails {
  /// Documented RondiTrack factory behavior.
  const factory StokvelDetails({
    /// Documented RondiTrack required behavior.
    required HomeEntry home,
    required List<ContributionEntry> contributions,
  }) = _StokvelDetails;
}
