/// Human-readable, typed failures. UI never checks HTTP status or Dio types.
sealed class Failure implements Exception {
  const Failure({this.reference});
  final String? reference;
  String get message;
  bool get canRetry => this is OfflineFailure;
  @override
  String toString() => message;
}

final class ValidationFailure extends Failure {
  const ValidationFailure(this.detail, {this.fields = const {}, super.reference});
  final String detail;
  final Map<String, List<String>> fields;
  @override
  String get message => detail.isEmpty ? 'Check the information you entered.' : detail;
}

final class UnauthorisedFailure extends Failure {
  const UnauthorisedFailure({super.reference});
  @override
  String get message => 'Please sign in again.';
}

final class ForbiddenFailure extends Failure {
  const ForbiddenFailure({super.reference});
  @override
  String get message => 'You do not have permission to do that.';
}

final class NotFoundFailure extends Failure {
  const NotFoundFailure({super.reference});
  @override
  String get message => 'Not found';
}

final class ConflictFailure extends Failure {
  const ConflictFailure({super.reference});
  @override
  String get message => 'This was changed elsewhere. Refresh and try again.';
}

final class RuleFailure extends Failure {
  const RuleFailure(this.detail, {super.reference});
  final String detail;
  @override
  String get message => detail.isEmpty ? 'This action cannot be completed.' : detail;
}

final class RateLimitedFailure extends Failure {
  const RateLimitedFailure(this.waitSeconds, {super.reference});
  final int? waitSeconds;
  @override
  String get message => waitSeconds == null
    ? 'Too many requests. Try again later.'
    : 'Too many requests. Try again in $waitSeconds seconds.';
}

final class ServerFailure extends Failure {
  const ServerFailure({super.reference});
  @override
  String get message => 'Something went wrong on our side. Please try later.';
}

final class OfflineFailure extends Failure {
  const OfflineFailure({super.reference});
  @override
  String get message => "You're offline, or we couldn't reach the server.";
}

final class UnexpectedFailure extends Failure {
  const UnexpectedFailure({super.reference});
  @override
  String get message => 'We could not read the server response.';
}
