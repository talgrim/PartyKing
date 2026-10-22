import {Typography} from '@mui/material';

interface InfoMessageProps {
  message: string;
}

export const InfoMessage = ({message}: InfoMessageProps) => {
  return (
    <Typography
      variant='h5'
      textAlign='center'
      m={2}
      sx={{
        color: (theme) => theme.palette.common.black,
      }}
    >
      {message}
    </Typography>
  );
};
