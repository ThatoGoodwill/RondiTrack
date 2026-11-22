import 'package:freezed_annotation/freezed_annotation.dart';

part 'wire_models.freezed.dart';
part 'wire_models.g.dart';

/// Exact API wire types, kept out of domain and presentation.
/// JSON generation is provided by Freezed + json_serializable.
@freezed
abstract class HomeWireResponse with _$HomeWireResponse {
  const factory HomeWireResponse({
    required List<HomeStokvelWire> stokvels,
  }) = _HomeWireResponse;

  factory HomeWireResponse.fromJson(Map<String, dynamic> json) =>
      _$HomeWireResponseFromJson(json);
}

@freezed
abstract class HomeStokvelWire with _$HomeStokvelWire {
  const factory HomeStokvelWire({
    required String stokvelId,
    required String name,
    required String role,
    required HomeCycleWire? currentCycle,
    required int arrearsCents,
    required String? nextPayoutDueUtc,
  }) = _HomeStokvelWire;

  factory HomeStokvelWire.fromJson(Map<String, dynamic> json) =>
      _$HomeStokvelWireFromJson(json);
}

@freezed
abstract class HomeCycleWire with _$HomeCycleWire {
  const factory HomeCycleWire({
    required String id,
    required String label,
    required int paidCents,
    required int owedCents,
    required String status,
    required String? endsOn,
  }) = _HomeCycleWire;

  factory HomeCycleWire.fromJson(Map<String, dynamic> json) =>
      _$HomeCycleWireFromJson(json);
}

@freezed
abstract class ContributionWire with _$ContributionWire {
  const factory ContributionWire({
    required String id,
    required String userId,
    required String stokvelId,
    required String contributionCycleId,
    required num amount,
    required String recordedAt,
  }) = _ContributionWire;

  factory ContributionWire.fromJson(Map<String, dynamic> json) =>
      _$ContributionWireFromJson(json);
}

@freezed
abstract class ContributionPageWire with _$ContributionPageWire {
  const factory ContributionPageWire({
    required List<ContributionWire> items,
    required String? nextPageToken,
  }) = _ContributionPageWire;

  factory ContributionPageWire.fromJson(Map<String, dynamic> json) =>
      _$ContributionPageWireFromJson(json);
}

@freezed
abstract class PayoutOrderWire with _$PayoutOrderWire {
  const factory PayoutOrderWire({
    required String stokvelId,
    required String cycleId,
    required String? nextMemberId,
    required List<PayoutEntryWire> entries,
  }) = _PayoutOrderWire;

  factory PayoutOrderWire.fromJson(Map<String, dynamic> json) =>
      _$PayoutOrderWireFromJson(json);
}

@freezed
abstract class PayoutEntryWire with _$PayoutEntryWire {
  const factory PayoutEntryWire({
    required String memberId,
    required String memberName,
    required int position,
    required int amountCents,
    required String status,
    required String? paidOnUtc,
  }) = _PayoutEntryWire;

  factory PayoutEntryWire.fromJson(Map<String, dynamic> json) =>
      _$PayoutEntryWireFromJson(json);
}

@freezed
abstract class TokenWire with _$TokenWire {
  const factory TokenWire({
    required String accessToken,
    required String expiresAt,
  }) = _TokenWire;

  factory TokenWire.fromJson(Map<String, dynamic> json) =>
      _$TokenWireFromJson(json);
}

@freezed
abstract class ActivityWire with _$ActivityWire {
  const factory ActivityWire({
    required String id,
    required String stokvelId,
    required String kind,
    required String occurredAt,
    required int? amountCents,
    required String description,
  }) = _ActivityWire;

  factory ActivityWire.fromJson(Map<String, dynamic> json) =>
      _$ActivityWireFromJson(json);
}

@freezed
abstract class ActivityPageWire with _$ActivityPageWire {
  const factory ActivityPageWire({
    required List<ActivityWire> items,
    required String? nextPageToken,
  }) = _ActivityPageWire;

  factory ActivityPageWire.fromJson(Map<String, dynamic> json) =>
      _$ActivityPageWireFromJson(json);
}
