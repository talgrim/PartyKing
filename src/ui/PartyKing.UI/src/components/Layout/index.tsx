import { FC, PropsWithChildren } from 'react';
import Stack from '@mui/material/Stack';
import { AppHeader } from '@/components/Layout/AppHeader';
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
        {!isDevEmbedded && <AppHeader />}
        {children}
      </Stack>
    </Stack>
  );
};
