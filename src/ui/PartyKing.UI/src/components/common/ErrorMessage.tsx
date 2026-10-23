import {Typography} from '@mui/material';

interface ErrorMessageProps {
  message: string;
}

export const ErrorMessage = ({message}: ErrorMessageProps) => {
  return (
    <Typography
      variant='h5'
      textAlign='center'
      m={2}
      sx={{
        color: (theme) => theme.palette.error.main,
      }}
    >
      Error occurred: {message}
    </Typography>
  );
};
