import { FC, PropsWithChildren } from 'react';
import Stack from '@mui/material/Stack';
import { SiteBar } from '@/components/Layout/SiteBar';
import { isDevEmbedded } from '@/service/devPersona';
import { usePageTitle } from '@/components/Layout/usePageTitle';
import { PrivacyFooter } from '@/components/Layout/PrivacyFooter';
import { PrivacyNotice } from '@/components/Layout/PrivacyNotice';

export const Layout: FC<PropsWithChildren> = ({ children }) => {
  usePageTitle();

  return (
    <Stack minHeight="100dvh" sx={{ backgroundColor: (theme) => theme.palette.background.default }}>
      {!isDevEmbedded && <SiteBar />}
      <Stack flexGrow={1} gap={1} alignItems="center" component="main">
        {children}
      </Stack>
      <PrivacyFooter />
      <PrivacyNotice />
    </Stack>
  );
};
