import 'package:freezed_annotation/freezed_annotation.dart';
import '../../../core/money/cents.dart';

part 'payout_models.freezed.dart';

/// Proposed payout-order contract, pending separate backend endpoint/PR.
/// Not a representation of the existing single-payment PayoutResponse.
enum PayoutStatus {
  paid('Paid'), next('Next'), waiting('Waiting'), unknown('Unknown');
  const PayoutStatus(this.apiValue);
  final String apiValue;
  static PayoutStatus fromApi(String value) => switch (value) {
    'Paid' => paid, 'Next' => next, 'Waiting' => waiting, _ => unknown,
  };
  String get label => switch (this) {
    paid => 'Paid', next => 'Next to receive',
    waiting => 'Waiting', unknown => 'Unknown payout status',
  };
}

@freezed
abstract class PayoutOrderEntry with _$PayoutOrderEntry {
  const factory PayoutOrderEntry({required String memberId, required String memberName,
    required int position, required Cents amountCents,
    required PayoutStatus status, String? paidOnUtc}) = _PayoutOrderEntry;
}

@freezed
abstract class PayoutOrder with _$PayoutOrder {
  const factory PayoutOrder({required String stokvelId, required String cycleId,
    required List<PayoutOrderEntry> entries, String? nextMemberId}) = _PayoutOrder;
}
