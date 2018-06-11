using System;
using System.Collections.Generic;
using System.Linq;
using NadekoBot.Common;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;

namespace NadekoBot.Modules.Games.Common.Trivia
{
    public class TriviaQuestionPool
    {
        private readonly IDataCache _cache;
        private readonly int maxPokemonId;
        private readonly TriviaQuestion[] Pool;

        private readonly NadekoRandom _rng = new NadekoRandom();

        private IReadOnlyDictionary<int, string> Map => _cache.LocalData.PokemonMap;

        public TriviaQuestionPool(IDataCache cache,  string category = null)
        {
            _cache = cache;
            maxPokemonId = 721; //xd

            var triviaQuestions = cache.LocalData.TriviaQuestions;
            if (string.IsNullOrEmpty(category))
            {
                Pool = triviaQuestions;
            }
            else
            {
                Pool = Array.FindAll(triviaQuestions, q => q.Category.ToLower().Equals(category.ToLower()));
            }
        }

        public TriviaQuestion GetRandomQuestion(HashSet<TriviaQuestion> exclude, bool isPokemon)
        {
            if (Pool.Length == 0)
                return null;
            if (Pool.Length == exclude.Count)
                // when all questions are finished
                return null;

            if (isPokemon)
            {
                var num = _rng.Next(1, maxPokemonId + 1);
                return new TriviaQuestion("Who's That Pokémon?", 
                    Map[num].ToTitleCase(),
                    "Pokemon",
                    $@"http://nadekobot.me/images/pokemon/shadows/{num}.png",
                    $@"http://nadekobot.me/images/pokemon/real/{num}.png");
            }

            TriviaQuestion randomQuestion;
            while (exclude.Contains(randomQuestion = Pool[_rng.Next(0, Pool.Length)])) ;

            return randomQuestion;
        }

        public string[] GetSortedCategoryList()
        {
            string[] categories = Pool.Select(q => q.Category).ToArray().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Array.Sort(categories, StringComparer.InvariantCulture);
            return categories;
        }
    }
}
