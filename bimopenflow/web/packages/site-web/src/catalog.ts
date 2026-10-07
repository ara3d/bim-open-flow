// The node catalog the page's static API serves: the generic host's answer to
// GET /api/catalog/nodes, committed beside the graph package's tests and kept
// current by GenericNodeCatalogFileTests (tests/flow/BimOpenFlow.Host.Tests).
// The build bundles it, so the page makes one request for data and none for
// the catalog.
import type { NodeCatalog } from "@bimopenflow/contracts";
import committed from "../../graph/test/nodes.catalog.json";

export const nodeCatalog = committed as NodeCatalog;
