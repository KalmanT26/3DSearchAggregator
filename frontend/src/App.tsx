import { BrowserRouter, Routes, Route } from 'react-router-dom';
import SearchPage from './pages/SearchPage';
import './index.css';

import LandingPage from './pages/LandingPage';

import AuthGate from './components/AuthGate';

function App() {
  return (
    <BrowserRouter>
      <AuthGate>
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/search" element={<SearchPage />} />
        </Routes>
      </AuthGate>
    </BrowserRouter>
  );
}

export default App;
