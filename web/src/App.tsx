import { NavLink, Navigate, Route, Routes } from 'react-router-dom';
import ListPage from './pages/ListPage';
import UploadPage from './pages/UploadPage';
import ReviewPage from './pages/ReviewPage';
import CompaniesPage from './pages/CompaniesPage';

export default function App() {
  return (
    <div className="layout">
      <header className="topbar">
        <h1>Delivery Note OCR</h1>
        <nav>
          <NavLink to="/notes" className={({ isActive }) => (isActive ? 'active' : '')}>
            Notes
          </NavLink>
          <NavLink to="/upload" className={({ isActive }) => (isActive ? 'active' : '')}>
            Upload
          </NavLink>
          <NavLink to="/companies" className={({ isActive }) => (isActive ? 'active' : '')}>
            Companies
          </NavLink>
        </nav>
      </header>
      <main className="container">
        <Routes>
          <Route path="/" element={<Navigate to="/notes" replace />} />
          <Route path="/notes" element={<ListPage />} />
          <Route path="/notes/:id" element={<ReviewPage />} />
          <Route path="/upload" element={<UploadPage />} />
          <Route path="/companies" element={<CompaniesPage />} />
        </Routes>
      </main>
    </div>
  );
}
