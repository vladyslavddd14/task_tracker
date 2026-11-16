import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Loading } from '../../components/Feedback';
import { useAuth } from './AuthContext';

export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'loading') return <Loading />;
  if (status === 'anonymous') return <Navigate to="/login" state={{ from: location }} replace />;
  return <Outlet />;
}
