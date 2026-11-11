import 'statuses.dart';

enum StokvelAction { pay, uploadProof, confirmContribution, closeCycle, recordPayout }

/// No catch-all over the statuses: adding a new case requires a decision here.
Set<StokvelAction> permittedActions(
  (StokvelRole, CycleStatus, ContributionStatus) context,
) => switch (context) {
  (StokvelRole.member, CycleStatus.open, ContributionStatus.notRecorded) =>
    {StokvelAction.pay},
  (StokvelRole.member, CycleStatus.open, ContributionStatus.recorded) =>
    {StokvelAction.uploadProof},
  (StokvelRole.member, CycleStatus.open, ContributionStatus.unknown) => {},
  (StokvelRole.treasurer || StokvelRole.admin, CycleStatus.open, ContributionStatus.notRecorded) =>
    {StokvelAction.pay, StokvelAction.confirmContribution, StokvelAction.closeCycle},
  (StokvelRole.treasurer || StokvelRole.admin, CycleStatus.open, ContributionStatus.recorded) =>
    {StokvelAction.uploadProof, StokvelAction.confirmContribution, StokvelAction.closeCycle},
  (StokvelRole.treasurer || StokvelRole.admin, CycleStatus.open, ContributionStatus.unknown) =>
    {StokvelAction.confirmContribution, StokvelAction.closeCycle},
  (StokvelRole.member, CycleStatus.closed, ContributionStatus.notRecorded) => {},
  (StokvelRole.member, CycleStatus.closed, ContributionStatus.recorded) => {},
  (StokvelRole.member, CycleStatus.closed, ContributionStatus.unknown) => {},
  (StokvelRole.treasurer || StokvelRole.admin, CycleStatus.closed, ContributionStatus.notRecorded) =>
    {StokvelAction.recordPayout},
  (StokvelRole.treasurer || StokvelRole.admin, CycleStatus.closed, ContributionStatus.recorded) =>
    {StokvelAction.recordPayout},
  (StokvelRole.treasurer || StokvelRole.admin, CycleStatus.closed, ContributionStatus.unknown) =>
    {StokvelAction.recordPayout},
  (StokvelRole.member || StokvelRole.treasurer || StokvelRole.admin, CycleStatus.paidOut,
    ContributionStatus.notRecorded || ContributionStatus.recorded || ContributionStatus.unknown) => {},
  (StokvelRole.unknown, CycleStatus.open || CycleStatus.closed || CycleStatus.paidOut,
    ContributionStatus.notRecorded || ContributionStatus.recorded || ContributionStatus.unknown) => {},
  (StokvelRole.member || StokvelRole.treasurer || StokvelRole.admin || StokvelRole.unknown,
    CycleStatus.unknown, ContributionStatus.notRecorded || ContributionStatus.recorded || ContributionStatus.unknown) => {},
};
