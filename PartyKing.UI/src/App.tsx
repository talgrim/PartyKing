import {Layout} from './components/Layout';
import {ComposeComponents} from "@/utils/ComposeComponents";
import {Outlet} from "react-router-dom";

// import {Provider} from "@/contexts/auth/provider";

export function App() {
  return (
    <ComposeComponents
      components={[
        // Provider
      ]}
    >
      <Layout>
        <Outlet/>
      </Layout>
    </ComposeComponents>
  );
}