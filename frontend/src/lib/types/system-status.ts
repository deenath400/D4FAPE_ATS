export type SystemStatusDto = {
  version: string;
  database: {
    reachable: boolean;
    schemaCurrent: boolean;
  };
};
