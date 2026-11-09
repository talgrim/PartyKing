import { FC, PropsWithChildren } from 'react';
import Stack from '@mui/material/Stack';
import { AccountMenu } from '@/components/Account/AccountMenu';
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher';
import { isDevEmbedded } from '@/service/devPersona';

export const Layout: FC<PropsWithChildren> = ({ children }) => {
  return (
    <Stack minHeight='100dvh'>
      <Stack
        flexGrow={1}
        gap={1}
        alignItems='center'
        component='main'
        sx={{ backgroundColor: (theme) => theme.palette.background.default }}
      >
        {!isDevEmbedded && <LanguageSwitcher />}
        {!isDevEmbedded && <AccountMenu />}
        {children}
      </Stack>
    </Stack>
  );
};
