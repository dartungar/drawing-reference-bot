public interface IRandomDrawingTopicService
{
    string GetRandomTopic();
}

public sealed class RandomDrawingTopicService : IRandomDrawingTopicService
{
    private static readonly string[] Topics =
    [
        "hands", "portrait", "eyes", "nose", "ears", "figure drawing", "gesture drawing", "seated figure", "running pose",
        "cat sleeping", "dog portrait", "wolf", "fox", "owl", "eagle", "snake", "frog", "butterfly", "dragon",
        "chair", "teapot", "shoes", "watch", "guitar", "violin", "bicycle", "vintage car", "book", "chess pieces",
        "oak tree", "rose", "mountains", "waterfall", "clouds", "storm clouds", "ocean waves", "rocks", "castle", "bridge",
        "street scene", "cityscape", "apple", "banana", "tomato", "bread loaf", "coffee cup", "fabric folds", "wood grain",
        "still life with fruit", "candlelit scene", "bird's eye view", "winter snow scene", "moonlit night",
        "old library interior", "busy train station", "rainy alley", "market stall", "fountain in a plaza",
        "desert dunes", "pine forest", "jungle path", "volcanic landscape", "cliffside coast",
        "sunset over lake", "foggy morning field", "night city lights", "reflections in puddles", "harbor at dawn",
        "koi fish", "horse running", "deer in forest", "elephant portrait", "lion resting",
        "rabbit", "squirrel", "parrot", "flamingo", "whale tail",
        "human skull study", "anatomy torso study", "arm muscles", "leg anatomy", "feet study",
        "draped cloth", "glass bottle", "metal kettle", "ceramic mug", "old lantern",
        "stack of books", "crumpled paper", "keys and lock", "camera on table", "headphones",
        "comic-style hero pose", "knight armor", "samurai silhouette", "wizard staff", "spacesuit figure",
        "robot concept", "steampunk goggles", "futuristic motorcycle", "airship", "fantasy doorway",
        "treehouse", "windmill", "lighthouse", "abandoned factory", "greenhouse",
        "kitchen corner", "bathroom sink", "bedroom with window light", "artist studio", "cozy cafe interior",
        "hands holding cup", "person reading", "person tying shoes", "person stretching", "dancer pose",
        "silhouette against sunset", "rim lighting portrait", "high contrast portrait", "soft window light portrait", "backlit hair study"
    ];

    public string GetRandomTopic() => Topics[Random.Shared.Next(Topics.Length)];
}
