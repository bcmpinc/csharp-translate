public class Player : MonoBehaviour
{
  private SpriteRenderer spriteRenderer;
  public Sprite[] sprites;
  private int spriteIndex;

  public float strength = 5f;

  private Vector3 direction;

  private void Awake()
  {
    spriteRenderer = GetComponent<SpriteRenderer>();
  }

  private void Start()
  {
    InvokeRepeating(
        nameof(AnimateSprite),
        0.15f,
        0.15f
    );
  }

  private void OnEnable()
  {
    Vector3 position = transform.position;
    position.y = 0f;
    transform.position = position;
    direction = Vector3.zero;
  }
}
