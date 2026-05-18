export const ROLE_DONOR = "donor";
export const ROLE_COORDINATOR = "coordinator";

export function isValidRole(role) {
  return role === ROLE_DONOR || role === ROLE_COORDINATOR;
}
