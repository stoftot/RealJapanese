using DataLoaders.Models;
using Repositories.Bases;

namespace Repositories;

public class ExpressionData : WordDataBase<Expression>
{
    public ExpressionData() : this(RepositoryPaths.Default) { }

    public ExpressionData(RepositoryPaths paths) : base(paths, "Expressions", "Expressions.json") { }
}
