# Convert unity .cs files to Godot 4 Scripts

This is a proof of concept for converting unity scripts to godot. It's far from perfect, but might still be useful to some.

It differs from [unifree](https://github.com/ProjectUnifree/unifree) in that instead of using AI, it parses the c# code and walks the AST to translate it in GDScript, more or less. This means it is deterministic, lightweigt, fast and works off-line. Furthermore it's relatively easy to make specific changes to what output it generates.

Feel free to pick this up and make it your own project as I've no plans to develop this any further. If you do, please let me know so I can add a link to your project in this readme.

## Usage

If you have dotnet installed, you can run it using:

```
dotnet run godot tests/Player.cs
```

There's also a debug mode, which basically prints the AST.

```
dotnet run debug tests/Player.cs
```

Output is send to stdout

## Example

```
Unity                                                | Godot
---------------------------------------------------- | ---------------------------------------------------------
public class Player : MonoBehaviour                  | # Converted from 'tests/Player.cs' using csharp-translate
{                                                    | extends Node
  private SpriteRenderer spriteRenderer;             |
  public Sprite[] sprites;                           | # class_name Player
  private int spriteIndex;                           | # extends : MonoBehaviour
                                                     |
  public float strength = 5f;                        | var spriteRenderer: SpriteRenderer
                                                     | var sprites: Array[Sprite]
  private Vector3 direction;                         | var spriteIndex: int
                                                     |
  private void Awake()                               | var strength: float = 5.0
  {                                                  |
    spriteRenderer = GetComponent<SpriteRenderer>(); | var direction: Vector3
  }                                                  | func _init():
                                                     |     spriteRenderer = get_node("SpriteRenderer")
  private void Start()                               |
  {                                                  | func _ready():
    InvokeRepeating(                                 |     InvokeRepeating(nameof(AnimateSprite), 0.15f, 0.15f)
        nameof(AnimateSprite),                       |
        0.15f,                                       | func OnEnable():
        0.15f                                        |     var position: Vector3 = transform.position
    );                                               |     position.y = 0.0
  }                                                  |     transform.position = position
                                                     |     direction = Vector3.zero
  private void OnEnable()                            |
  {                                                  | # end of class Player
    Vector3 position = transform.position;           |
    position.y = 0f;                                 |
    transform.position = position;                   |
    direction = Vector3.zero;                        |
  }                                                  |
}                                                    |
```

## License
Creative Commons Attribution 4.0 (CC BY 4.0)

Please contact me if you'd like to use it under a different license.
