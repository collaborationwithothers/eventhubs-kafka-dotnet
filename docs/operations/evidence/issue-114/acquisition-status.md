# Acquisition status provenance

The acquisition status summary is authored evidence. Its [unchanged JSON](chart-tool-acquisition-status.json) and [historical producer record](acquisition-status-provenance.json) are kept in reviewed scope alongside the original command captures.

The [official Helm release response](raw/chart-tool-acquisition/helm-release-4.3.1.json) supplies the release version and archive checksum. The [Helm](raw/chart-tool-acquisition/helm-host-denial.txt) and [service-tags](raw/chart-tool-acquisition/service-tags-host-denial.txt) captures record destination denials; the latter is a host-reachability probe. The [SDK publish log](raw/deployment-attempt-2/images/loadgen-publish.log) reaches runtime restore/build and fails while retrieving the container base image. The [build log](raw/deployment-tests-final/build.log) retains the separate successful compilation.

The historical summary's destination list is an authored conclusion. A [fresh MCR host probe](raw/placement-fix/mcr-host-probe.txt) confirms a proxy CONNECT denial for `centralus.data.mcr.microsoft.com`. This probe uses no Azure credentials or live resources. Deployment acceptance remains incomplete as the [evidence README](README.md) records.
