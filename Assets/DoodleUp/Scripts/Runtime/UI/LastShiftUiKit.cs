using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 아이콘 게이지 8종. <b>색이 아니라 실루엣으로 갈린다</b> — 32px 에서도 서로 다른
    /// 모양이라 색각 이상에서도 구분이 남는다(키트 §"방향").
    ///
    /// 축마다 <c>base</c>(빈 외곽선)와 <c>fill</c>(컬러 채움) 두 장이 <b>같은 좌표에</b>
    /// 겹친다. 가로 막대가 없어진 자리를 아이콘 자체가 대신한다.
    /// </summary>
    public enum LastShiftUiIcon
    {
        Maintenance,
        Materials,
        Oxygen,
        Food,
        Docking,
        Thrust,
        Interact,
        Warning
    }

    /// <summary>
    /// 게이지 <b>채움</b> 갈래. 아이콘(<see cref="LastShiftUiIcon"/>)과 <b>따로 세는 이유</b>는
    /// 채움 그림이 아이콘보다 적기 때문이다 — 키트에는 채움이 다섯 장뿐이고(정비여력·자재·
    /// 산소·식량·도킹), 추력·상호작용·경고는 자기 채움 없이 남의 채움을 빌려 쓴다.
    ///
    /// 둘을 한 열거형으로 합치면 채움이 없는 값이 <c>FillOf</c> 에서 조용히 도킹으로 떨어져,
    /// 화면에는 뜨는데 색이 틀린 상태가 된다. 갈래를 갈라 두면 빌려 쓰는 것이 호출부에 보인다.
    /// </summary>
    public enum LastShiftGaugeChannel
    {
        Maintenance,
        Materials,
        Oxygen,
        Food,
        Docking
    }

    /// <summary>
    /// UI 아트 키트 v1 의 스프라이트 묶음.
    ///
    /// <b>왜 <see cref="ScriptableObject"/> 인가.</b> 그림은 <c>Assets/DoodleUp/Art/UI/LastShift/</c>
    /// 에 있고 그 경로는 아트 카드가 문서로 약속한 자리라 옮길 수 없다. 그런데 런타임은
    /// <c>Resources</c> 밖의 에셋을 이름으로 못 집는다. 그래서 <b>참조만 들고 있는 에셋</b>을
    /// <c>Resources</c> 에 두고, 그림 자체는 제자리에 남긴다.
    ///
    /// 에셋은 손으로 만들지 않는다 — <c>LastShiftUiKitBuilder</c>(에디터)가 경로 표를 보고
    /// 굽는다. 아이콘이 하나 늘면 표에 한 줄을 더하고 다시 굽는 것이 전부다.
    ///
    /// <b>키트가 없어도 UI 는 뜬다.</b> 스프라이트가 <c>null</c> 이면 단색 사각형으로 떨어지고
    /// 배치·값은 그대로다. 테스트가 에셋 없이도 돌아야 하기 때문이다.
    /// </summary>
    public sealed class LastShiftUiKit : ScriptableObject
    {
        /// <summary><c>Resources.Load</c> 에 쓰는 이름. 빌더와 런타임이 같은 상수를 본다.</summary>
        public const string ResourcePath = "LastShiftUiKit";

        [Header("아이콘 128×128")]
        [SerializeField] private Sprite iconMaintenance;
        [SerializeField] private Sprite iconMaterials;
        [SerializeField] private Sprite iconOxygen;
        [SerializeField] private Sprite iconFood;
        [SerializeField] private Sprite iconDocking;
        [SerializeField] private Sprite iconThrust;
        [SerializeField] private Sprite iconInteract;
        [SerializeField] private Sprite iconWarning;

        [Header("게이지 채움 128×128 — 위 아이콘과 같은 좌표에 겹친다")]
        [SerializeField] private Sprite fillMaintenance;
        [SerializeField] private Sprite fillMaterials;
        [SerializeField] private Sprite fillOxygen;
        [SerializeField] private Sprite fillFood;
        [SerializeField] private Sprite fillDocking;

        [Header("패널·프롬프트")]
        [SerializeField] private Sprite panel9Slice;
        [SerializeField] private Sprite promptPlate;
        [SerializeField] private Sprite keycap;

        private static LastShiftUiKit cached;
        private static bool lookupAttempted;

        /// <summary>
        /// 키트 한 벌. 없으면 <c>null</c> 을 돌려주고 <b>다시 찾지 않는다</b> — 매 프레임
        /// <c>Resources.Load</c> 를 때리면 없는 에셋을 찾느라 프레임마다 디스크를 훑는다.
        /// </summary>
        public static LastShiftUiKit Instance
        {
            get
            {
                if (cached != null) return cached;
                if (lookupAttempted) return null;
                lookupAttempted = true;
                cached = Resources.Load<LastShiftUiKit>(ResourcePath);
                return cached;
            }
        }

        /// <summary>테스트가 만든 임시 키트를 꽂는다. 에셋 없이도 스프라이트 배선을 검증하려는 자리다.</summary>
        public static void OverrideForTests(LastShiftUiKit kit)
        {
            cached = kit;
            lookupAttempted = kit != null;
        }

        /// <summary>캐시를 비운다. 다음 접근에서 다시 찾는다.</summary>
        public static void ResetLookup()
        {
            cached = null;
            lookupAttempted = false;
        }

        public Sprite Panel => panel9Slice;
        public Sprite PromptPlate => promptPlate;
        public Sprite Keycap => keycap;

        /// <summary>
        /// 프롬프트판은 <b>가로로 늘어난다</b> — 문장 길이가 문마다 다르다. 늘어나도 모서리가
        /// 안 눌리도록 9-slice 경계를 <b>임포트 시점에</b> 박아 둔다
        /// (<c>LastShiftUiKitBuilder.BorderOf</c>). 여기서는 그 스프라이트를 그대로 쓴다.
        ///
        /// <b>게이지에는 늘어나는 판이 없다.</b> 가로 막대를 없애고 아이콘 한 장으로 줄인
        /// 것이 키트 v1 의 결정이라(문서 §"트레이드오프"), 늘어나는 것은 판과 프롬프트뿐이다.
        /// </summary>
        public Sprite SlicedPromptPlate => promptPlate;

        public Sprite IconOf(LastShiftUiIcon icon) => icon switch
        {
            LastShiftUiIcon.Maintenance => iconMaintenance,
            LastShiftUiIcon.Materials => iconMaterials,
            LastShiftUiIcon.Oxygen => iconOxygen,
            LastShiftUiIcon.Food => iconFood,
            LastShiftUiIcon.Docking => iconDocking,
            LastShiftUiIcon.Thrust => iconThrust,
            LastShiftUiIcon.Interact => iconInteract,
            _ => iconWarning
        };

        public Sprite FillOf(LastShiftGaugeChannel channel) => channel switch
        {
            LastShiftGaugeChannel.Maintenance => fillMaintenance,
            LastShiftGaugeChannel.Materials => fillMaterials,
            LastShiftGaugeChannel.Oxygen => fillOxygen,
            LastShiftGaugeChannel.Food => fillFood,
            _ => fillDocking
        };

        /// <summary>
        /// 빌더가 쓰는 주입구. 필드를 <c>public</c> 으로 열면 런타임 어디서나 키트를 바꿔
        /// 쓸 수 있게 되므로, 굽는 쪽만 쓰는 함수 하나로 좁혀 둔다.
        /// </summary>
        public void Assign(string field, Sprite sprite)
        {
            switch (field)
            {
                case nameof(iconMaintenance): iconMaintenance = sprite; break;
                case nameof(iconMaterials): iconMaterials = sprite; break;
                case nameof(iconOxygen): iconOxygen = sprite; break;
                case nameof(iconFood): iconFood = sprite; break;
                case nameof(iconDocking): iconDocking = sprite; break;
                case nameof(iconThrust): iconThrust = sprite; break;
                case nameof(iconInteract): iconInteract = sprite; break;
                case nameof(iconWarning): iconWarning = sprite; break;
                case nameof(fillMaintenance): fillMaintenance = sprite; break;
                case nameof(fillMaterials): fillMaterials = sprite; break;
                case nameof(fillOxygen): fillOxygen = sprite; break;
                case nameof(fillFood): fillFood = sprite; break;
                case nameof(fillDocking): fillDocking = sprite; break;
                case nameof(panel9Slice): panel9Slice = sprite; break;
                case nameof(promptPlate): promptPlate = sprite; break;
                case nameof(keycap): keycap = sprite; break;
                default: Debug.LogWarning($"[LastShiftUiKit] 모르는 칸: {field}"); break;
            }
        }

        /// <summary>
        /// 빌더와 테스트가 같이 보는 표. <b>칸 이름과 파일 이름을 여기서만 잇는다</b> —
        /// 두 곳에 적으면 파일을 하나 바꿀 때 한쪽만 고쳐진다.
        /// </summary>
        public static readonly (string Field, string FileName)[] SpriteTable =
        {
            // 이름은 <c>Tools/art/generate_last_shift_ui_kit.py</c> 의 <c>icon_pair</c> 가 짓는
            // 그대로다. 축 하나가 <c>_base</c>(빈 외곽선)와 <c>_fill</c>(컬러 채움) 두 장으로
            // 오고, 둘은 같은 좌표에 겹쳐 쓰라고 만들어졌다.
            (nameof(iconMaintenance), "icon_gauge_maintenance_base"),
            (nameof(iconMaterials), "icon_gauge_materials_base"),
            (nameof(iconOxygen), "icon_gauge_oxygen_base"),
            (nameof(iconFood), "icon_gauge_food_base"),
            (nameof(iconDocking), "icon_gauge_docking_base"),
            (nameof(iconThrust), "icon_gauge_thrust_base"),
            (nameof(iconInteract), "icon_gauge_interact_base"),
            (nameof(iconWarning), "icon_gauge_warning_base"),
            (nameof(fillMaintenance), "icon_gauge_maintenance_fill"),
            (nameof(fillMaterials), "icon_gauge_materials_fill"),
            (nameof(fillOxygen), "icon_gauge_oxygen_fill"),
            (nameof(fillFood), "icon_gauge_food_fill"),
            (nameof(fillDocking), "icon_gauge_docking_fill"),
            (nameof(panel9Slice), "panel_9slice"),
            (nameof(promptPlate), "prompt_plate"),
            (nameof(keycap), "keycap")
        };

        /// <summary>키트 그림이 사는 폴더. 빌더가 여기서 읽는다.</summary>
        public const string ArtFolder = "Assets/DoodleUp/Art/UI/LastShift";

        /// <summary>구워진 에셋이 놓이는 자리.</summary>
        public const string AssetPath = "Assets/DoodleUp/Resources/LastShiftUiKit.asset";

        /// <summary>배선이 한 칸이라도 비었는지. 빌더와 EditMode 검사가 같이 쓴다.</summary>
        public bool IsFullyWired()
        {
            foreach (var (field, _) in SpriteTable)
                if (SpriteOfField(field) == null)
                    return false;
            return true;
        }

        public Sprite SpriteOfField(string field) => field switch
        {
            nameof(iconMaintenance) => iconMaintenance,
            nameof(iconMaterials) => iconMaterials,
            nameof(iconOxygen) => iconOxygen,
            nameof(iconFood) => iconFood,
            nameof(iconDocking) => iconDocking,
            nameof(iconThrust) => iconThrust,
            nameof(iconInteract) => iconInteract,
            nameof(iconWarning) => iconWarning,
            nameof(fillMaintenance) => fillMaintenance,
            nameof(fillMaterials) => fillMaterials,
            nameof(fillOxygen) => fillOxygen,
            nameof(fillFood) => fillFood,
            nameof(fillDocking) => fillDocking,
            nameof(panel9Slice) => panel9Slice,
            nameof(promptPlate) => promptPlate,
            nameof(keycap) => keycap,
            _ => null
        };
    }
}
