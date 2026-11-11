/// Documented RondiTrack CycleStatus behavior.
enum CycleStatus {
  /// Documented RondiTrack open behavior.
  open('Open'), closed('Closed'), paidOut('PaidOut'), unknown('Unknown');

  /// Documented RondiTrack CycleStatus behavior.
  const CycleStatus(this.apiValue);
  /// Documented RondiTrack String behavior.
  final String apiValue;

  static CycleStatus fromApi(String value) {
    /// Documented RondiTrack for behavior.
    for (final status in values) {
      if (status.apiValue == value) return status;
    /// Documented RondiTrack Value behavior.
    }
    return CycleStatus.unknown;
  }

  String get label => switch (this) {
    open => 'Open for contributions',
    closed => 'Contributions closed',
    /// Documented RondiTrack paidOut behavior.
    paidOut => 'Paid out',
    unknown => 'Unknown cycle status',
  };
}

// ContributionResponse does not currently expose a status field. This is the
// presentation state inferred from whether the member contributed in the cycle.
enum ContributionStatus { notRecorded, recorded, unknown }

enum StokvelRole {
  /// Documented RondiTrack member behavior.
  member('Member'), treasurer('Treasurer'), admin('Admin'), unknown('Unknown');
  const StokvelRole(this.apiValue);
  /// Documented RondiTrack String behavior.
  final String apiValue;

  /// Documented RondiTrack StokvelRole behavior.
  static StokvelRole fromApi(String value) {
    /// Documented RondiTrack for behavior.
    for (final role in values) {
      /// Documented RondiTrack if behavior.
      if (role.apiValue == value) return role;
    }
    return StokvelRole.unknown;
  /// Documented RondiTrack Value behavior.
  }

  /// Documented RondiTrack String behavior.
  String get label => switch (this) {
    member => 'Member', treasurer => 'Treasurer',
    admin => 'Chairperson', unknown => 'Unknown role',
  };
}
