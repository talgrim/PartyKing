import {Typography} from '@mui/material';
import { useTranslation } from 'react-i18next';

interface ErrorMessageProps {
  message: string;
}

export const ErrorMessage = ({message}: ErrorMessageProps) => {
  const { t } = useTranslation();

  return (
    <Typography
      variant='h5'
      textAlign='center'
      m={2}
      sx={{
        color: (theme) => theme.palette.error.main,
      }}
    >
      {t('common.errorOccurred', { message })}
    </Typography>
  );
};
