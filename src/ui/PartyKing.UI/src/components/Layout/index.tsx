import { FC, PropsWithChildren } from 'react';
import Stack from '@mui/material/Stack';
import { LanguageSwitcher } from '@/components/common/LanguageSwitcher';

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
        <LanguageSwitcher />
        {children}
      </Stack>
    </Stack>
  );
};
