import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';

/// All paths live in this routing boundary; feature widgets use these
/// address objects rather than writing URL strings or using Navigator.push.
sealed class AppDestination {
  const AppDestination();
  String get location;
  void go(BuildContext context) => context.go(location);
  void push(BuildContext context) => context.push(location);
}
final class HomeDestination extends AppDestination {
  const HomeDestination();
  @override String get location => '/home';
}
final class StokvelListDestination extends AppDestination {
  const StokvelListDestination();
  @override String get location => '/stokvels';
}
final class StokvelDestination extends AppDestination {
  const StokvelDestination(this.id);
  final String id;
  @override String get location => '/stokvels/${Uri.encodeComponent(id)}';
}
final class CycleDestination extends AppDestination {
  const CycleDestination(this.stokvelId, this.cycleId);
  final String stokvelId, cycleId;
  @override String get location =>
    '/stokvels/${Uri.encodeComponent(stokvelId)}/cycles/${Uri.encodeComponent(cycleId)}';
}
final class PayoutDestination extends AppDestination {
  const PayoutDestination(this.stokvelId, this.cycleId);
  final String stokvelId, cycleId;
  @override String get location =>
    '/stokvels/${Uri.encodeComponent(stokvelId)}/cycles/${Uri.encodeComponent(cycleId)}/payouts';
}
final class ActivityDestination extends AppDestination {
  const ActivityDestination();
  @override String get location => '/activity';
}
final class ProfileDestination extends AppDestination {
  const ProfileDestination();
  @override String get location => '/profile';
}
final class JoinDestination extends AppDestination {
  const JoinDestination(this.code);
  final String code;
  @override String get location => '/join/${Uri.encodeComponent(code)}';
}
