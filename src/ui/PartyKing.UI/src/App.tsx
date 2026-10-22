import {Layout} from './components/Layout';
import {ComposeComponents} from "@/utils/ComposeComponents";
import {Outlet} from "react-router-dom";
import {createTheme, CssBaseline, ThemeProvider} from "@mui/material";

// import {Provider} from "@/contexts/auth/provider";

const themeDark = createTheme({
  palette: {
    background: {
      default: "#000000"
    },
    text: {
      primary: "#ffffff",
    }
  }
})

export function App() {
  return (
    <ThemeProvider theme={themeDark}>
      <CssBaseline />
      <ComposeComponents
        components={[
          // Provider
        ]}
      >
        <Layout>
          <Outlet/>
        </Layout>
      </ComposeComponents>
    </ThemeProvider>
  );
}