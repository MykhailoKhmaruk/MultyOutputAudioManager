using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace MultyAudioOutput.Localization;

public sealed record LanguageOption(string Code, string NativeName);

/// <summary>
/// UI strings. XAML binds through <see cref="TrExtension"/> ({l:Tr key}); code uses <see cref="T"/> / <see cref="F"/>.
/// Switching the language updates every bound text immediately.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string RussianEasterEggUrl = "https://www.youtube.com/watch?v=RgdANpB9PnY";
    private const string Glory1 = "Слава Україні!";
    private const string Glory2 = "Героям Слава!";

    public static Loc I { get; } = new();

    /// <summary>Order shown in the pickers. Names stay native so you can always switch back.</summary>
    public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("pt", "Português"),
        new LanguageOption("en", "English"),
        new LanguageOption("zh", "中文"),
        new LanguageOption("uk", "Українська"),
        new LanguageOption("es", "Español"),
        new LanguageOption("ru", "Русский"),
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? LanguageChanged;

    public string Language { get; private set; } = "uk";
    public bool IsRussian => Language == "ru";

    public string this[string key] => T(key);

    /// <summary>System UI language if supported (Russian falls back to English), else English.</summary>
    public static string DefaultLanguage
    {
        get
        {
            var code = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return code != "ru" && Strings.ContainsKey(code) ? code : "en";
        }
    }

    /// <summary>Applies a language. <paramref name="userChoice"/> = picked by the user just now (not loaded from settings).</summary>
    public void SetLanguage(string code, bool userChoice)
    {
        if (code != "ru" && !Strings.ContainsKey(code)) code = "en";
        bool changed = code != Language;
        Language = code;

        if (changed)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke();
        }

        if (userChoice && code == "ru")
        {
            try { Process.Start(new ProcessStartInfo(RussianEasterEggUrl) { UseShellExecute = true }); }
            catch { /* no browser */ }
        }
    }

    public static string T(string key)
    {
        if (I.IsRussian) return Glory(key);
        if (Strings.TryGetValue(I.Language, out var table) && table.TryGetValue(key, out var text)) return text;
        return Strings["en"].TryGetValue(key, out var en) ? en : key;
    }

    public static string F(string key, params object[] args) =>
        I.IsRussian ? Glory(key) : string.Format(T(key), args);

    /// <summary>Real names (apps, devices) — replaced too when Russian is selected.</summary>
    public static string Name(string realName) => I.IsRussian ? Glory(realName) : realName;

    private static string Glory(string seed)
    {
        int sum = 0;
        foreach (var c in seed) sum += c;
        return sum % 2 == 0 ? Glory1 : Glory2;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["uk"] = new()
        {
            ["flyout.title"] = "Пристрої виводу",
            ["flyout.subtitle"] = "Перетягніть програму на потрібний пристрій",
            ["flyout.settings"] = "Налаштування",
            ["flyout.empty"] = "Зараз жодна програма не відтворює звук — запустіть відео чи гру.",
            ["flyout.hotkeyHint"] = "{0} — перемкнути вихід програми в активному вікні",
            ["flyout.systemBadge"] = "системний",
            ["flyout.dropHere"] = "Перетягніть сюди програму",
            ["flyout.resetDefault"] = "Повернути на системний пристрій",
            ["flyout.remembered"] = "Вибір запам'ятовано",
            ["flyout.auto"] = "авто",
            ["flyout.autoTip"] = "Слідує за системним пристроєм Windows",
            ["flyout.playing"] = "Зараз грає",
            ["flyout.mute"] = "Вимкнути звук",
            ["flyout.unmute"] = "Увімкнути звук",
            ["tray.tooltip"] = "MultyAudioOutput — вивід звуку для програм",
            ["tray.open"] = "Відкрити панель",
            ["tray.settings"] = "Налаштування…",
            ["tray.autostart"] = "Запускати разом з Windows",
            ["tray.language"] = "Мова",
            ["tray.exit"] = "Вихід",
            ["tray.runningTitle"] = "MultyAudioOutput працює в треї",
            ["tray.runningText"] = "Клацніть іконку навушників, щоб вибрати вивід звуку для програм.",
            ["tray.unsupported"] = "Ця версія Windows не підтримує вибір виходу звуку для окремих програм.",
            ["tray.hotkeysTitle"] = "Гарячі клавіші",
            ["tray.hotkeysFailed"] = "Не вдалося зареєструвати: {0}. Змініть їх у налаштуваннях.",
            ["tray.soundTo"] = "Звук → {0}",
            ["settings.title"] = "MultyAudioOutput — налаштування",
            ["settings.general"] = "Загальне",
            ["settings.autostart"] = "Запускати разом з Windows",
            ["settings.remember"] = "Запам'ятовувати вибраний пристрій для кожної програми",
            ["settings.language"] = "Мова",
            ["settings.hotkeys"] = "Гарячі клавіші",
            ["settings.cycle"] = "Перемкнути вихід активної програми на наступний пристрій",
            ["settings.flyout"] = "Показати / сховати панель",
            ["settings.hotkeyHint"] = "Клацніть у поле й натисніть комбінацію (з Ctrl / Alt / Shift / Win). Backspace — вимкнути.",
            ["settings.saved"] = "Збережено.",
            ["settings.hotkeyTaken"] = "Не вдалося зареєструвати {0} — комбінація зайнята іншою програмою. Спробуйте іншу.",
            ["settings.off"] = "вимкнено",
            ["settings.rules"] = "Збережені правила",
            ["settings.noRules"] = "Поки що порожньо. Перетягніть програму на пристрій у панелі — правило з'явиться тут.",
            ["settings.deleteRule"] = "Видалити правило",
            ["settings.notConnected"] = "· не підключено",
            ["settings.reset"] = "Скидання",
            ["settings.resetText"] = "Видалити всі правила й повернути всі програми на системний пристрій (також скидає налаштування Windows «Гучність програм»).",
            ["settings.resetButton"] = "Скинути все",
            ["settings.resetConfirm"] = "Видалити всі збережені правила й повернути всі програми на системний пристрій виводу?",
            ["settings.close"] = "Закрити",
        },
        ["en"] = new()
        {
            ["flyout.title"] = "Output devices",
            ["flyout.subtitle"] = "Drag an app onto the device you want",
            ["flyout.settings"] = "Settings",
            ["flyout.empty"] = "No app is playing sound right now — start a video or a game.",
            ["flyout.hotkeyHint"] = "{0} — switch the output of the app in the active window",
            ["flyout.systemBadge"] = "system",
            ["flyout.dropHere"] = "Drop an app here",
            ["flyout.resetDefault"] = "Back to the system device",
            ["flyout.remembered"] = "Choice remembered",
            ["flyout.auto"] = "auto",
            ["flyout.autoTip"] = "Follows the Windows default device",
            ["flyout.playing"] = "Playing now",
            ["flyout.mute"] = "Mute",
            ["flyout.unmute"] = "Unmute",
            ["tray.tooltip"] = "MultyAudioOutput — per-app sound output",
            ["tray.open"] = "Open panel",
            ["tray.settings"] = "Settings…",
            ["tray.autostart"] = "Start with Windows",
            ["tray.language"] = "Language",
            ["tray.exit"] = "Exit",
            ["tray.runningTitle"] = "MultyAudioOutput is running in the tray",
            ["tray.runningText"] = "Click the headphones icon to choose where each app's sound goes.",
            ["tray.unsupported"] = "This Windows version doesn't support choosing the output device per app.",
            ["tray.hotkeysTitle"] = "Hotkeys",
            ["tray.hotkeysFailed"] = "Couldn't register: {0}. Change them in Settings.",
            ["tray.soundTo"] = "Sound → {0}",
            ["settings.title"] = "MultyAudioOutput — settings",
            ["settings.general"] = "General",
            ["settings.autostart"] = "Start with Windows",
            ["settings.remember"] = "Remember the chosen device for each app",
            ["settings.language"] = "Language",
            ["settings.hotkeys"] = "Hotkeys",
            ["settings.cycle"] = "Move the active app to the next output device",
            ["settings.flyout"] = "Show / hide the panel",
            ["settings.hotkeyHint"] = "Click the box and press a combination (with Ctrl / Alt / Shift / Win). Backspace turns it off.",
            ["settings.saved"] = "Saved.",
            ["settings.hotkeyTaken"] = "Couldn't register {0} — another app already uses it. Try a different one.",
            ["settings.off"] = "off",
            ["settings.rules"] = "Saved rules",
            ["settings.noRules"] = "Nothing yet. Drag an app onto a device in the panel and the rule will appear here.",
            ["settings.deleteRule"] = "Delete rule",
            ["settings.notConnected"] = "· not connected",
            ["settings.reset"] = "Reset",
            ["settings.resetText"] = "Delete all rules and send every app back to the system device (also resets Windows \"App volume\" settings).",
            ["settings.resetButton"] = "Reset all",
            ["settings.resetConfirm"] = "Delete all saved rules and send every app back to the system output device?",
            ["settings.close"] = "Close",
        },
        ["es"] = new()
        {
            ["flyout.title"] = "Dispositivos de salida",
            ["flyout.subtitle"] = "Arrastra una aplicación al dispositivo que quieras",
            ["flyout.settings"] = "Configuración",
            ["flyout.empty"] = "Ninguna aplicación reproduce sonido ahora — inicia un vídeo o un juego.",
            ["flyout.hotkeyHint"] = "{0} — cambiar la salida de la aplicación de la ventana activa",
            ["flyout.systemBadge"] = "sistema",
            ["flyout.dropHere"] = "Suelta una aplicación aquí",
            ["flyout.resetDefault"] = "Volver al dispositivo del sistema",
            ["flyout.remembered"] = "Elección guardada",
            ["flyout.auto"] = "auto",
            ["flyout.autoTip"] = "Sigue el dispositivo predeterminado de Windows",
            ["flyout.playing"] = "Reproduciendo",
            ["flyout.mute"] = "Silenciar",
            ["flyout.unmute"] = "Activar sonido",
            ["tray.tooltip"] = "MultyAudioOutput — salida de sonido por aplicación",
            ["tray.open"] = "Abrir panel",
            ["tray.settings"] = "Configuración…",
            ["tray.autostart"] = "Iniciar con Windows",
            ["tray.language"] = "Idioma",
            ["tray.exit"] = "Salir",
            ["tray.runningTitle"] = "MultyAudioOutput está en la bandeja",
            ["tray.runningText"] = "Haz clic en el icono de auriculares para elegir la salida de sonido de cada aplicación.",
            ["tray.unsupported"] = "Esta versión de Windows no permite elegir la salida de sonido por aplicación.",
            ["tray.hotkeysTitle"] = "Atajos de teclado",
            ["tray.hotkeysFailed"] = "No se pudo registrar: {0}. Cámbialos en la configuración.",
            ["tray.soundTo"] = "Sonido → {0}",
            ["settings.title"] = "MultyAudioOutput — configuración",
            ["settings.general"] = "General",
            ["settings.autostart"] = "Iniciar con Windows",
            ["settings.remember"] = "Recordar el dispositivo elegido para cada aplicación",
            ["settings.language"] = "Idioma",
            ["settings.hotkeys"] = "Atajos de teclado",
            ["settings.cycle"] = "Pasar la aplicación activa al siguiente dispositivo",
            ["settings.flyout"] = "Mostrar / ocultar el panel",
            ["settings.hotkeyHint"] = "Haz clic en el campo y pulsa una combinación (con Ctrl / Alt / Shift / Win). Retroceso la desactiva.",
            ["settings.saved"] = "Guardado.",
            ["settings.hotkeyTaken"] = "No se pudo registrar {0}: otra aplicación ya la usa. Prueba otra.",
            ["settings.off"] = "desactivado",
            ["settings.rules"] = "Reglas guardadas",
            ["settings.noRules"] = "Aún no hay nada. Arrastra una aplicación a un dispositivo en el panel y la regla aparecerá aquí.",
            ["settings.deleteRule"] = "Eliminar regla",
            ["settings.notConnected"] = "· no conectado",
            ["settings.reset"] = "Restablecer",
            ["settings.resetText"] = "Eliminar todas las reglas y devolver todas las aplicaciones al dispositivo del sistema (también restablece «Volumen de aplicaciones» de Windows).",
            ["settings.resetButton"] = "Restablecer todo",
            ["settings.resetConfirm"] = "¿Eliminar todas las reglas guardadas y devolver todas las aplicaciones al dispositivo de salida del sistema?",
            ["settings.close"] = "Cerrar",
        },
        ["pt"] = new()
        {
            ["flyout.title"] = "Dispositivos de saída",
            ["flyout.subtitle"] = "Arraste um aplicativo para o dispositivo desejado",
            ["flyout.settings"] = "Configurações",
            ["flyout.empty"] = "Nenhum aplicativo está reproduzindo som agora — inicie um vídeo ou um jogo.",
            ["flyout.hotkeyHint"] = "{0} — trocar a saída do aplicativo da janela ativa",
            ["flyout.systemBadge"] = "sistema",
            ["flyout.dropHere"] = "Solte um aplicativo aqui",
            ["flyout.resetDefault"] = "Voltar ao dispositivo do sistema",
            ["flyout.remembered"] = "Escolha memorizada",
            ["flyout.auto"] = "auto",
            ["flyout.autoTip"] = "Segue o dispositivo padrão do Windows",
            ["flyout.playing"] = "Tocando agora",
            ["flyout.mute"] = "Silenciar",
            ["flyout.unmute"] = "Ativar som",
            ["tray.tooltip"] = "MultyAudioOutput — saída de som por aplicativo",
            ["tray.open"] = "Abrir painel",
            ["tray.settings"] = "Configurações…",
            ["tray.autostart"] = "Iniciar com o Windows",
            ["tray.language"] = "Idioma",
            ["tray.exit"] = "Sair",
            ["tray.runningTitle"] = "MultyAudioOutput está na bandeja",
            ["tray.runningText"] = "Clique no ícone de fones de ouvido para escolher a saída de som de cada aplicativo.",
            ["tray.unsupported"] = "Esta versão do Windows não permite escolher a saída de som por aplicativo.",
            ["tray.hotkeysTitle"] = "Teclas de atalho",
            ["tray.hotkeysFailed"] = "Não foi possível registrar: {0}. Altere-as nas configurações.",
            ["tray.soundTo"] = "Som → {0}",
            ["settings.title"] = "MultyAudioOutput — configurações",
            ["settings.general"] = "Geral",
            ["settings.autostart"] = "Iniciar com o Windows",
            ["settings.remember"] = "Lembrar o dispositivo escolhido para cada aplicativo",
            ["settings.language"] = "Idioma",
            ["settings.hotkeys"] = "Teclas de atalho",
            ["settings.cycle"] = "Mover o aplicativo ativo para o próximo dispositivo",
            ["settings.flyout"] = "Mostrar / ocultar o painel",
            ["settings.hotkeyHint"] = "Clique no campo e pressione uma combinação (com Ctrl / Alt / Shift / Win). Backspace desativa.",
            ["settings.saved"] = "Salvo.",
            ["settings.hotkeyTaken"] = "Não foi possível registrar {0} — outro aplicativo já a usa. Tente outra.",
            ["settings.off"] = "desativado",
            ["settings.rules"] = "Regras salvas",
            ["settings.noRules"] = "Nada ainda. Arraste um aplicativo para um dispositivo no painel e a regra aparecerá aqui.",
            ["settings.deleteRule"] = "Excluir regra",
            ["settings.notConnected"] = "· não conectado",
            ["settings.reset"] = "Redefinir",
            ["settings.resetText"] = "Excluir todas as regras e devolver todos os aplicativos ao dispositivo do sistema (também redefine o «Volume de aplicativos» do Windows).",
            ["settings.resetButton"] = "Redefinir tudo",
            ["settings.resetConfirm"] = "Excluir todas as regras salvas e devolver todos os aplicativos ao dispositivo de saída do sistema?",
            ["settings.close"] = "Fechar",
        },
        ["zh"] = new()
        {
            ["flyout.title"] = "输出设备",
            ["flyout.subtitle"] = "将应用拖到所需的设备上",
            ["flyout.settings"] = "设置",
            ["flyout.empty"] = "当前没有应用在播放声音——请播放视频或启动游戏。",
            ["flyout.hotkeyHint"] = "{0} — 切换当前窗口应用的输出设备",
            ["flyout.systemBadge"] = "系统",
            ["flyout.dropHere"] = "将应用拖到这里",
            ["flyout.resetDefault"] = "恢复为系统设备",
            ["flyout.remembered"] = "已记住选择",
            ["flyout.auto"] = "自动",
            ["flyout.autoTip"] = "跟随 Windows 默认设备",
            ["flyout.playing"] = "正在播放",
            ["flyout.mute"] = "静音",
            ["flyout.unmute"] = "取消静音",
            ["tray.tooltip"] = "MultyAudioOutput — 按应用选择声音输出",
            ["tray.open"] = "打开面板",
            ["tray.settings"] = "设置…",
            ["tray.autostart"] = "开机时启动",
            ["tray.language"] = "语言",
            ["tray.exit"] = "退出",
            ["tray.runningTitle"] = "MultyAudioOutput 正在托盘中运行",
            ["tray.runningText"] = "点击耳机图标，为每个应用选择声音输出。",
            ["tray.unsupported"] = "此 Windows 版本不支持为单个应用选择输出设备。",
            ["tray.hotkeysTitle"] = "快捷键",
            ["tray.hotkeysFailed"] = "无法注册：{0}。请在设置中更改。",
            ["tray.soundTo"] = "声音 → {0}",
            ["settings.title"] = "MultyAudioOutput — 设置",
            ["settings.general"] = "常规",
            ["settings.autostart"] = "开机时启动",
            ["settings.remember"] = "记住每个应用选择的设备",
            ["settings.language"] = "语言",
            ["settings.hotkeys"] = "快捷键",
            ["settings.cycle"] = "将当前应用切换到下一个输出设备",
            ["settings.flyout"] = "显示 / 隐藏面板",
            ["settings.hotkeyHint"] = "点击输入框并按下组合键（需含 Ctrl / Alt / Shift / Win）。按 Backspace 关闭。",
            ["settings.saved"] = "已保存。",
            ["settings.hotkeyTaken"] = "无法注册 {0}——该组合键已被其他应用占用。请换一个。",
            ["settings.off"] = "关闭",
            ["settings.rules"] = "已保存的规则",
            ["settings.noRules"] = "暂无规则。在面板中将应用拖到某个设备上，规则就会出现在这里。",
            ["settings.deleteRule"] = "删除规则",
            ["settings.notConnected"] = "· 未连接",
            ["settings.reset"] = "重置",
            ["settings.resetText"] = "删除所有规则，并将所有应用恢复为系统设备（同时重置 Windows“应用音量”设置）。",
            ["settings.resetButton"] = "全部重置",
            ["settings.resetConfirm"] = "删除所有已保存的规则，并将所有应用恢复为系统输出设备？",
            ["settings.close"] = "关闭",
        },
    };
}

/// <summary>XAML: Text="{l:Tr flyout.title}" — live-updates when the language changes.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Loc.I, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
