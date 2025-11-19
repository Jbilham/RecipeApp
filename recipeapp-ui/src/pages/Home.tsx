import { Link } from 'react-router-dom';
export default function Home() {
  const cards = [
    { title: 'Upload Recipe', link: '/upload-recipe', color: 'from-blue-500 to-blue-700' },
    { title: 'Upload Meal Plan', link: '/upload-mealplan', color: 'from-yellow-400 to-yellow-600' },
    { title: 'TrainingPeaks Import', link: '/import', color: 'from-purple-500 to-purple-700' },
    { title: 'View Shopping Lists', link: '/shopping-lists', color: 'from-green-500 to-green-700' }
  ];
  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gradient-to-b from-gray-100 to-gray-200">
      <h1 className="text-3xl font-bold mb-10 text-gray-800">Welcome to RecipeApp</h1>
      <div className="grid grid-cols-1 md:grid-cols-3 gap-8 max-w-5xl">
        {cards.map((card) => (
          <Link key={card.link} to={card.link}>
            <div className={`p-10 rounded-2xl bg-gradient-to-r ${card.color} text-white shadow-lg transform hover:scale-105 transition-all duration-300`}>
              <h2 className="text-2xl font-semibold text-center">{card.title}</h2>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}
