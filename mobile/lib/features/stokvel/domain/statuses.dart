enum CycleStatus {
  open('Open'), closed('Closed'), paidOut('PaidOut'), unknown('Unknown');

  const CycleStatus(this.apiValue);
  final String apiValue;

  static CycleStatus fromApi(String value) {
    for (final status in values) {
      if (status.apiValue == value) return status;
    }
    return CycleStatus.unknown;
  }

  String get label => switch (this) {
    open => 'Open for contributions',
    closed => 'Contributions closed',
    paidOut => 'Paid out',
    unknown => 'Unknown cycle status',
  };
}

// ContributionResponse does not currently expose a status field. This is the
// presentation state inferred from whether the member contributed in the cycle.
enum ContributionStatus { notRecorded, recorded, unknown }

enum StokvelRole {
  member('Member'), treasurer('Treasurer'), admin('Admin'), unknown('Unknown');
  const StokvelRole(this.apiValue);
  final String apiValue;

  static StokvelRole fromApi(String value) {
    for (final role in values) {
      if (role.apiValue == value) return role;
    }
    return StokvelRole.unknown;
  }

  String get label => switch (this) {
    member => 'Member', treasurer => 'Treasurer',
    admin => 'Chairperson', unknown => 'Unknown role',
  };
}
