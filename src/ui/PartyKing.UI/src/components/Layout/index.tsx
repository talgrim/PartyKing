import { FC, PropsWithChildren } from 'react';
import Stack from '@mui/material/Stack';

export const Layout: FC<PropsWithChildren> = ({ children }) => {
  return (
    <Stack height='100vh' overflow='auto'>
      <Stack
        flexGrow={1}
        overflow='hidden'
        minHeight={540}
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
