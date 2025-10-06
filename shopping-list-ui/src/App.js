import React, { useEffect, useState } from "react";

function App() {
  const [recipes, setRecipes] = useState([]);
  const [selected, setSelected] = useState([]);
  const [shoppingList, setShoppingList] = useState(null);

  useEffect(() => {
    fetch("http://localhost:5114/api/recipes")
      .then(res => res.json())
      .then(setRecipes);
  }, []);

  const toggleRecipe = (id) => {
    setSelected(prev =>
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );
  };

  const buildShoppingList = () => {
    fetch("http://localhost:5114/api/shoppinglist", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ recipeIds: selected })
    })
      .then(res => res.json())
      .then(setShoppingList);
  };

  return (
    <div style={{ padding: 20 }}>
      <h1>Shopping List App</h1>

      <h2>Recipes</h2>
      <ul>
        {recipes.map(r => (
          <li key={r.id}>
            <label>
              <input
                type="checkbox"
                checked={selected.includes(r.id)}
                onChange={() => toggleRecipe(r.id)}
              />
              {r.title}
            </label>
          </li>
        ))}
      </ul>

      <button onClick={buildShoppingList} disabled={!selected.length}>
        Build Shopping List
      </button>

      {shoppingList && (
        <div>
          <h2>Shopping List</h2>
          <ul>
            {shoppingList.items.map((i, idx) => (
              <li key={idx}>
                {i.amount ?? ""} {i.unit ?? ""} {i.ingredient}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

export default App;
