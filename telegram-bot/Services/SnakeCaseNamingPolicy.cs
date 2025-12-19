using System.Text;
using System.Text.Json;

namespace TelegramBot.Services;

public sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current))
            {
                var hasPrevious = i > 0;
                var hasNext = i < name.Length - 1;
                var previous = hasPrevious ? name[i - 1] : '\0';
                var next = hasNext ? name[i + 1] : '\0';

                var shouldInsertSeparator = hasPrevious &&
                                            (char.IsLower(previous) ||
                                             char.IsDigit(previous) ||
                                             (char.IsUpper(previous) && hasNext && char.IsLower(next)));

                if (shouldInsertSeparator)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
