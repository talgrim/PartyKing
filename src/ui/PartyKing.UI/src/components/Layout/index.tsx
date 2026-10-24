import { FC, PropsWithChildren } from 'react';
import Stack from '@mui/material/Stack';

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
        {children}
      </Stack>
    </Stack>
  );
};
