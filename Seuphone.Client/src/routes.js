import { Switch, Route } from 'react-router-dom';
import { Cart } from './pages/Cart';

import { Home } from './pages/Home';
import { Login } from './pages/Login';
import { ProductDetail } from './pages/ProductDetail';
import { Products } from './pages/Products';
import { Profile } from './pages/Profile';
import { Register } from './pages/Register';

export function Routes() {
  return (
    <Switch>
      <Route path="/" exact component={Home} />

      <Route path="/login" exact component={Login} />
      <Route path="/register" exact component={Register} />
      <Route path="/profile" exact component={Profile} />

      <Route path="/products" exact component={Products} />
      <Route path="/products/:id" exact component={ProductDetail} />
      
      <Route path="/cart" exact component={Cart} />
    </Switch>
  );
};