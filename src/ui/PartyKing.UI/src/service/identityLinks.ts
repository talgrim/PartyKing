import envVariables from '@/envVariables';

/** Where the visitor lands after logging in: the page they came from. */
const next = () => encodeURIComponent(window.location.href);

/**
 * Links to the pages of the identity service, which owns the accounts (login, registration, settings, the admin's
 * list of users). `?next=` brings the visitor back here.
 */
export const identityLinks = {
  login: () => `${envVariables.identityUrl}/login?next=${next()}`,
  register: () => `${envVariables.identityUrl}/register?next=${next()}`,
  settings: () => `${envVariables.identityUrl}/account`,
  adminUsers: () => `${envVariables.identityUrl}/admin/users`,
};
