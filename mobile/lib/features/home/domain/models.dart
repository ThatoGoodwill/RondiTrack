import 'package:freezed_annotation/freezed_annotation.dart';
import '../../../core/money/cents.dart';
import '../../stokvel/domain/statuses.dart';

part 'models.freezed.dart';

// A membership owns the role. The user and access token have no role.
@freezed
abstract class Membership with _$Membership {
  const factory Membership({
    required String stokvelId,
    required StokvelRole role,
  }) = _Membership;
}

@freezed
abstract class CurrentCycle with _$CurrentCycle {
  const factory CurrentCycle({
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
  const factory HomeEntry({
    required Membership membership,
    required String name,
    required Cents arrearsCents,
    CurrentCycle? currentCycle,
    DateTime? nextPayoutDueUtc,
  }) = _HomeEntry;
}

@freezed
abstract class ContributionEntry with _$ContributionEntry {
  const factory ContributionEntry({
    required String id,
    required String userId,
    required Cents amountCents,
    required DateTime recordedAtUtc,
    required ContributionStatus status,
  }) = _ContributionEntry;
}

@freezed
abstract class StokvelDetails with _$StokvelDetails {
  const factory StokvelDetails({
    required HomeEntry home,
    required List<ContributionEntry> contributions,
  }) = _StokvelDetails;
}
