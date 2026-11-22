import '../../../core/network/failure.dart';

/// Week 10's domain-specific not-found also participates in Week 11's
/// unified Failure presentation. No caller can distinguish missing membership.
final class StokvelNotFound extends NotFoundFailure {
  const StokvelNotFound();
}
