using System;
using System.Collections.Generic;
using System.Linq;

namespace AgDns {
 // Interface language. Russian is the source text: every string the window shows goes through L.T(russian), which returns it unchanged
 // in Russian and looks it up here in English. A missing entry falls back to the Russian text (the self-test walks every window state in English
 // and fails on any Cyrillic it finds). The technical log, the manual recovery steps and the support report stay in Russian.
 public static class L {
  public static bool En;
  public static string T(string ru)=>En&&ru!=null&&Ui.TryGetValue(ru,out var e)?e:ru;
  public static string F(string ruFormat,params object[] args)=>string.Format(T(ruFormat),args);
  public static bool Has(string ru)=>Ui.ContainsKey(ru);
  // A phrase that is already on screen, written in the other language: Russian -> English through the dictionary, English -> Russian through its reverse.
  // Anything the dictionary does not know (a name, a number, a sentence built from parts) comes back unchanged.
  static Dictionary<string,string> reverse;
  static Dictionary<string,string> Reverse(){
   if(reverse!=null)return reverse;
   var r=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(var kv in Ui)if(!r.ContainsKey(kv.Value))r[kv.Value]=kv.Key;
   return reverse=r;
  }
  public static string Swap(string text,bool toEnglish){
   if(string.IsNullOrEmpty(text))return text;
   if(toEnglish)return Ui.TryGetValue(text,out var e)?e:text;
   return Reverse().TryGetValue(text,out var ru)?ru:text;
  }
  // English phrases that two Russian phrases share: a switch back to Russian could not tell them apart (the self-test requires none).
  public static IEnumerable<string> SharedEnglish()=>Ui.GroupBy(kv=>kv.Value).Where(g=>g.Count()>1).Select(g=>g.Key);
  public static bool HasCode(string code)=>Codes.ContainsKey(code);
  // The message table entry in the current language (the Russian entry itself when Russian is on or no translation exists).
  public static CodeInfo Code(CodeInfo ru){
   if(!En||ru==null||ru.Code==null||!Codes.TryGetValue(ru.Code,out var e))return ru;
   return new CodeInfo{Code=ru.Code,Title=e[0],Text=e[1],Action=e[2],Kind=ru.Kind};
  }

  static readonly Dictionary<string,string> Ui=new Dictionary<string,string>(StringComparer.Ordinal){
   // navigation and page titles
   {"Мост","Bridge"},{"Замер","Test"},{"Настройки","Settings"},{"Логи","Logs"},{"Инфо","Info"},
   // main page
   {"Проверяю мост…","Checking the bridge…"},{"Мост подключён","Bridge connected"},{"Мост отключён","Bridge disconnected"},
   {"Проверяю, что мост работает…","Checking that the bridge works…"},{"Выполняю…","Working…"},{"Операция отменена","Operation cancelled"},
   {"Подключить мост","Connect bridge"},{"Отключить мост","Disconnect"},{"Восстановить","Restore"},{"Починить","Repair"},
   {"Подключить через …","Connect via …"},{"Подключить через ","Connect via "},{"Подробнее","Details"},
   {"Профиль","Profile"},{"Через мост","Route through the bridge"},{"+ Браузер","+ Browser"},{"Antigravity/Gemini/браузер","Antigravity/Gemini/browser"},
   {"Только домены Antigravity.","Only the Antigravity domains."},
   {"Antigravity, Gemini Desktop и сайт gemini.google.com.","Antigravity, Gemini Desktop and the gemini.google.com site."},
   {"Все домены Google: Gemini, NotebookLM, AI Studio, поиск, почта и остальные сервисы.","Every Google domain: Gemini, NotebookLM, AI Studio, search, mail and the other services."},

   {"измеряю…","measuring…"},{"не ответил","no answer"},{" мс"," ms"},
   {"быстро","fast"},{"нормально","normal"},{"медленно","slow"},
   {"Задержка до сервера-посредника провайдера: время TCP-соединения с адресом, который провайдер отдаёт для ","Latency to the provider's intermediary server: the TCP connect time to the address the provider gives for "},
   {"Проверяю провайдера…","Checking the provider…"},{"Проверяю службу WMI…","Checking the WMI service…"},{"Подтвердите запрос Windows…","Confirm the Windows prompt…"},
   {"Подключаю мост…","Connecting…"},{"Отключаю мост…","Disconnecting…"},{"Чиню мост…","Repairing…"},{"Проверяю результат…","Checking the result…"},
   {"Ищу рабочего провайдера…","Looking for a working provider…"},
   {"  ·  свой","  ·  own"},{"  ·  эксп.","  ·  exp."},
   // browser question (reconnect note)
   {"Мост подключится заново: один запрос Windows. Если не получится, прежний мост останется.","The bridge reconnects: one Windows prompt. If that fails, the current bridge stays."},
   // trust
   {"Провайдер «","The provider “"},
   {"» направит HTTPS-соединения доменов Google через свой сервер-посредник (SNI-прокси). Сертификат TLS проверяется, но провайдер видит имена хостов и объём трафика.\n\nДоверять провайдеру и подключить мост?","” will route HTTPS connections to Google domains through its intermediary server (SNI proxy). The TLS certificate is still verified, but the provider sees host names and traffic volume.\n\nTrust this provider and connect the bridge?"},
   {"Подтверждение доверия","Trust confirmation"},
   // tray
   {"Открыть окно","Open window"},{"Мост: проверяю…","Bridge: checking…"},{"Починить мост","Repair bridge"},{"Выход","Exit"},{"Мост: ","Bridge: "},
   {"требует починки","needs repair"},{"проверяю…","checking…"},{"подключён","connected"},{"требуется восстановление","needs restoring"},{"требует внимания","needs attention"},{"отключён","disconnected"},
   {"Antigravity DNS — мост подключён","Antigravity DNS — bridge connected"},{"Antigravity DNS — мост отключён","Antigravity DNS — bridge disconnected"},
   {"Antigravity DNS — требуется восстановление","Antigravity DNS — needs restoring"},{", пинг ",", ping "},
   {"Программа осталась в трее. Выход — в меню значка.","The program keeps running in the tray. Exit from the icon menu."},
   {"Операция ещё идёт. Прервать её и закрыть программу?","An operation is still running. Stop it and close the program?"},
   // benchmark
   {"Измерить","Measure"},{"Отмена","Cancel"},{"Отменяю…","Cancelling…"},{"Выбрать лучший","Pick the best"},
   {"Замер ничего не меняет в системе: только DoH-запросы и проверка TLS.","The test changes nothing in the system: only DoH queries and TLS checks."},
   {"Замер от ","Test of "},{" (прошлый запуск). Сеть могла измениться: нажмите «Измерить».",", previous run. The network may have changed: press “Measure”."},
   {"Повторный замер через "," Next test in "},{" с."," s."},
   {"Идёт замер: ","Testing: "},{" из "," of "},{" профилей, "," profiles, "},{" с (лимит 30 с). «Отмена» остановит замер."," s (limit 30 s). “Cancel” stops it."},
   {"Замер отменён. Показаны результаты до остановки.","Test cancelled. Results up to the stop are shown."},
   {"Лучший: ","Best: "},{" баллов · медиана "," points · median "},{" · не все профили успели (лимит "," · not every profile finished (limit "},{" с)"," s)"},
   {"Подходящего профиля нет: ни один не прошёл проверку всех доменов на этой сети.","No suitable profile: none passed the check for every domain on this network."},
   {"Экспериментальный профиль: не рекомендуется, пока замер не покажет «Работает».","Experimental profile: not recommended until the test shows “Works”."},
   {"не измерялся","not measured"},
   {"Работает","Works"},{"Медленно","Slow"},{"Нестабильно","Unstable"},{"Частично","Partial"},{"Недоступен","Unreachable"},{"Не измерен","Not measured"},{"Измеряю…","Measuring…"},
   {"ПРОФИЛЬ","PROFILE"},{"ДОСТ.","AVAIL."},{"МЕД. МС","MED. MS"},{"P95 МС","P95 MS"},{"TLS МС","TLS MS"},{"домены","domains"},
   {"Доступность: ","Availability: "},{" раундов)"," rounds)"},{"Задержка DoH: медиана ","DoH latency: median "},{"Стабильность ответов: ","Answer stability: "},
   {"достижимость адресов: ","address reachability: "},{"резолв + TLS ок (","resolve + TLS ok ("},{"резолв ок, TLS не прошёл","resolve ok, TLS failed"},{"нет ответа","no response"},
   // settings
   {"ЗАПУСК И ТРЕЙ","STARTUP AND TRAY"},{"ПРОГРАММА","PROGRAM"},
   {"Запускать при входе в Windows","Start with Windows"},{"Программа следит за состоянием моста","The program monitors the bridge"},
   {"При автозапуске — сразу в трей","Start in the tray"},{"Без окна, только значок у часов","No window, just the tray icon"},
   {"Крестик сворачивает в трей","Close button hides to the tray"},{"Выйти — через меню значка в трее","Exit from the tray icon menu"},
   {"Уведомлять о сбоях моста","Notify about bridge problems"},{"Сообщение, если мост перестал работать","A pop-up if the bridge stops working"},
   {"Язык","Language"},{"Свой DoH-провайдер","Own DoH provider"},{"Не задан: нажмите, чтобы добавить","Not set: click to add one"},
   {"Автозапуск ведёт на другую копию","Autostart points to another copy"},
   {"Закрыть","Close"},{"Хватит DoH-ссылки: адреса провайдера найдутся при подключении. Название и IP — по желанию.","A DoH link is enough: the provider's addresses are found when you connect. Name and IPs are optional."},
   {"DoH-ссылка  https://…/dns-query","DoH link  https://…/dns-query"},{"Название (необязательно)","Name (optional)"},{"IP-адреса (необязательно)","IP addresses (optional)"},
   {"Найти IP","Find IPs"},{"Ищу…","Searching…"},{"Сохранить","Save"},{"Сохранено","Saved"},{"Удалить","Delete"},
   {"Свой провайдер не входит в проверенные: программа проверит его так же строго (TLS по имени и DoH для всех доменов), но работу не гарантирует.","An own provider is not one of the tested ones: it is checked just as strictly (TLS on its name and DoH for every domain), but it is not guaranteed to work."},
   {"Введите DoH-ссылку с именем хоста, например https://dns.example/dns-query.","Enter a DoH link with a host name, for example https://dns.example/dns-query."},
   {"Найти IP не удалось: ","Could not find IPs: "},{"TLS по имени: ","TLS on name: "},{"ок","ok"},{"нет","no"},{"DoH для всех доменов: ","DoH for every domain: "},
   {"Хост ","Host "},{"  публичных IPv4 нет","  no public IPv4"},
   {"Ни один адрес не прошёл проверку TLS по имени и DoH для всех доменов. Закреплять нечего.","No address passed the TLS-on-name and DoH check for every domain. Nothing to pin."},
   {" (сверка с системным резолвером, публичными DoH-резолверами и провайдерами каталога):"," (cross-checked with the system resolver, public DoH resolvers and the catalog providers):"},
   {"Закрепить прошедшие проверку адреса?","Pin the addresses that passed?"},{"Адреса попадут в поле IP, а профиль сохранит кнопка «Сохранить».","The addresses go into the IP field; “Save” stores the profile."},
   {"DoH-ссылка должна начинаться с https:// и содержать имя хоста, а не IP.","The DoH link must start with https:// and contain a host name, not an IP."},
   {"Название не должно быть длиннее 28 символов.","The name must be at most 28 characters."},{"Можно указать не больше 4 IP-адресов.","At most 4 IP addresses."},
   {"Неверный или непубличный IP-адрес: ","Invalid or non-public IP address: "},
   // autostart
   {"Не удалось обновить запись автозапуска в реестре.","Could not update the autostart entry in the registry."},{"Автозапуск не изменён: ","Autostart not changed: "},
   {"Прежняя копия: ","Previous copy: "},{"Прежняя копия","Previous copy"},{"Удалить файл прежней копии?\n","Delete the previous copy?\n"},
   {"\n\nУдалится только этот файл программы, больше ничего.","\n\nOnly this program file is deleted, nothing else."},
   {"Прежняя копия сейчас запущена. Закройте её и повторите.","The previous copy is running. Close it and try again."},
   {"Это не файл этой программы: ничего не удалено.","That is not this program's file: nothing was deleted."},{"Не удалось удалить файл: ","Could not delete the file: "},
   // logs and details
   {"Диагностика","Diagnostics"},{"Копировать","Copy"},{"Скопировано","Copied"},{"Очистить","Clear"},{"Отчёт для поддержки","Support report"},
   {"Профиль: ","Profile: "},{" (экспериментальный: не рекомендуется, пока замер не покажет «Работает»)"," (experimental: not recommended until the test shows “Works”)"},
   {" (свой: не входит в официально поддерживаемые)"," (own: not one of the supported providers)"},{"Закреплённые адреса: ","Pinned addresses: "},{"нет, адреса ищутся при подключении","none, addresses are found when connecting"},
   {"Адреса: ","Addresses: "},{"Адреса: закреплённые","Addresses: pinned"},{"Адреса: найдены ранее","Addresses: found earlier"},{"Адреса: найдены сейчас, 12.10 15:30","Addresses: found now, 12.10 15:30"},
   {"найдены сейчас, ","found now, "},{"сохранённые, ","saved, "},{"закреплённые","pinned"},
   {"Сетевые проверки: ","Network checks: "},{"пройдены при подключении","passed when connecting"},{"не выполнялись (вкладка «Логи» → «Диагностика»)","not run (Logs tab → Diagnostics)"},
   {"Пинг до сервера-посредника провайдера: ","Ping to the provider's intermediary server: "},{" мс ("," ms ("},
   {"\r\nИзвестная проблема: ","\r\nKnown issue: "},{"\r\nЗаметка: ","\r\nNote: "},{"\r\nПоследнее сообщение: ","\r\nLast message: "},{"«Подробнее»","“Details”"},
   {"Что делать: ","What to do: "},{"Код: ","Code: "},{"\r\nИнструкция по ручному восстановлению:\r\n","\r\nManual recovery steps (in Russian):\r\n"},
   {"Дождитесь конца текущей операции и повторите.","Wait for the current operation to finish and try again."},
   {"Сохранить отчёт для поддержки","Save the support report"},{"Текстовый файл (*.txt)|*.txt","Text file (*.txt)|*.txt"},
   {"не выполнялась","not run"},{"свой профиль","own profile"},
   {"Предпросмотр отчёта","Report preview"},{"Сохранить…","Save…"},
   {"Файл будет сохранён в таком виде. Имя пользователя, имя компьютера, личные папки и имена сетей вырезаны. Отчёт никуда не отправляется.","The file is saved exactly like this. User name, computer name, personal folders and network names are removed. Nothing is sent anywhere."},
   // info
   {"Версия","Version"},
   {"Google закрывает Antigravity, Gemini и другие сервисы для ряда стран, например России. Только DNS-запросы к нужным доменам Google уходят зашифрованно (DoH) провайдеру; он отвечает адресом своего сервера-посредника. С Google соединение шифруется напрямую, провайдер видит лишь имя сайта.",
    "Google blocks Antigravity, Gemini and other services in some countries, such as Russia. Only DNS queries for the needed Google domains go encrypted (DoH) to your provider, which replies with its own intermediary server's address. Traffic to Google stays encrypted end to end; the provider sees only the site name."},
   {"Остальные сайты и VPN не меняются; при отключении удаляются только правила программы.","Other sites and VPNs are untouched; turning off removes only the program's own rules."},
   {"Российским аккаунтам для стабильной работы нужен ещё Antigravity Patcher.","Russian accounts also need Antigravity Patcher to work reliably."},
   {"Скачать Antigravity Patcher","Download Antigravity Patcher"},{"Автор патчера — если с ним что-то не так","Patcher author — if something is wrong with it"},
   {"СВЯЗАТЬСЯ","CONTACT"},{"Автор Antigravity DNS","Author of Antigravity DNS"},
   {"Адрес скопирован: вставьте его в адресную строку","Address copied: paste it into the address bar"},
   {"Какой браузер для Gemini и Google?","Which browser for Gemini and Google?"},
   {"Назад","Back"},
   {"Продолжить","Continue"},{"Готово","Done"},{"Открыть настройку браузера","Open browser settings"},{"Проверить снова","Check again"},
   {"Ищу браузеры…","Looking for browsers…"},{"Другой / не знаю","Other / not sure"},
   {"Выберите браузер — проверю, обходит ли он мост","Choose a browser — I will check whether it skips the bridge"},
   {"Google в браузере пойдёт через мост","Google in this browser will go through the bridge"},
   {"Свой «Безопасный DNS» браузера обходит мост","The browser's own “Secure DNS” skips the bridge"},
   {"«Безопасный DNS» задан политикой и обходит мост","“Secure DNS” set by a policy skips the bridge"},
   {"Не удалось определить: файл настроек не найден","Could not tell: settings file not found"},
   {"Не удалось определить: Firefox мог включить DoH сам","Could not tell: Firefox may turn on DoH by itself"},
   {"Не удалось определить: безопасный DNS без адреса","Could not tell: secure DNS has no server address"},
   {"Не удалось определить: надёжной проверки нет","Could not tell: no reliable check for this browser"},
   {"Проверьте «Безопасный DNS» в своём браузере","Check “Secure DNS” in your browser's settings"},
   {"Не удалось определить: браузер не проверяется","Could not tell: this browser cannot be checked"},
   {"Браузер для Google","Browser for Google"},{"Не выбран: нажмите, чтобы выбрать","Not chosen: click to choose"},{"проверить снова","check again"},
   // message table helpers (Messages.cs)
   {"Выберите ниже «Antigravity» или другой профиль.","Choose “Antigravity” below or another profile."},
   {"Профиль «{0}» не дал рабочих адресов, «{1}» прошёл проверку. Ничего не изменено.","Profile “{0}” gave no working addresses, “{1}” passed the check. Nothing was changed."},
   {"Профиль не применён","Profile not applied"},
   {"Новый профиль не применился, прежний вернуть не удалось. Мост отключён, записей программы нет.","The new profile was not applied and the previous one could not be brought back. The bridge is off; no program entries remain."},
   {"Повторите или выберите другой профиль.","Try again or pick another profile."},
  };

  // Title, text, action of every message code in English (Messages.cs holds the Russian table).
  static readonly Dictionary<string,string[]> Codes=new Dictionary<string,string[]>(StringComparer.Ordinal){
   {"NOT_CONFIGURED",new[]{"Bridge disconnected","The bridge is not set up; DNS settings were not changed.","Press “Connect bridge”."}},
   {"DNS_CONFIGURED_APPLICATION_NOT_TESTED",new[]{"Bridge connected","DNS for the Google domains is set. The program does not test Antigravity itself.","Open Antigravity and check sign-in."}},
   {"ROLLED_BACK",new[]{"Bridge disconnected","The program's entries were removed.","Nothing to do."}},
   {"ROLLED_BACK_SHARED_DOH_RETAINED",new[]{"Bridge disconnected","The program's entries were removed. A shared DoH entry other rules need was kept.","Nothing to do."}},
   {"LIVE_INTEGRATION_PASS",new[]{"Check passed","The check on this computer passed.","Nothing to do."}},
   {"RECOVERY_REQUIRED",new[]{"Restore needed","The last operation did not finish; some settings may remain.","Press “Restore”."}},
   {"FAILED_RESTORABLE",new[]{"Restore needed","Connecting failed and the rollback is incomplete.","Press “Restore”."}},
   {"OWNED_RULES_REMAIN",new[]{"Restore needed","The program's rules were not removed.","Press “Restore” again."}},
   {"DOH_RESTORE_FAILED",new[]{"Restore needed","The program's DoH entry was not removed.","Press “Restore” again."}},
   {"RESTORE_BEFORE_SCOPE_UPGRADE",new[]{"Restore needed","The old setup does not cover every domain.","Press “Restore”, then connect again."}},
   {"RESTORE_UNCONFIRMED",new[]{"Disconnect not confirmed","Windows did not confirm removing the entries in time. The journal was not changed.","Follow the steps in “Details”."}},
   {"WMI_NOT_RESPONDING",new[]{"WMI is not responding","The WMI service is not responding; nothing was started.","Restart the WMI service or the computer and try again."}},
   {"STATE_INVALID",new[]{"Journal unreadable","The program's journal cannot be read; settings were not changed.","Keep state.json and open “Details”."}},
   {"STATE_PATH_REPARSE_POINT",new[]{"Journal folder unsafe","The journal folder was replaced by a link; the program does not use it.","Remove the link yourself, see “Details”."}},
   {"STATE_DIR_UNTRUSTED",new[]{"Journal folder unsafe","The journal folder is not owned by an administrator.","Remove the folder yourself, see “Details”."}},
   {"NRPT_OR_HOSTS_CONFLICT",new[]{"Rule conflict","These domains are already set by another DNS rule or a hosts entry.","Remove the other rule or entry and try again."}},
   {"NRPT_CONFLICT",new[]{"Rule conflict","These domains are already set by another DNS rule or a hosts entry. Nothing was changed.","Remove the other rule or entry and try again."}},
   {"DOH_REGISTRATION_CONFLICT",new[]{"DoH entry conflict","Windows already has another DoH entry for the provider's address. Nothing was changed.","Pick another profile or remove the other entry."}},
   {"DOH_CHANGED_INDEPENDENTLY",new[]{"DoH entry changed","The program's DoH entry was changed outside it and was kept.","Check the entry, see “Details”."}},
   {"DOH_OWNERSHIP_UNCERTAIN",new[]{"DoH entry unclear","It is unclear whether the program created the DoH entry; it was kept.","Check the entry, see “Details”."}},
   {"OWNED_RULE_CHANGED",new[]{"Rule changed","The program's rule was edited by hand and was kept.","Check the rule, see “Details”."}},
   {"NRPT_NOT_EFFECTIVE",new[]{"Rules not in effect","The rules exist but are not in effect: a DNS cache, a VPN or an app with its own DNS interferes.","Press “Diagnostics” on the Logs tab."}},
   {"DOH_NO_VALIDATED_ENDPOINT",new[]{"Provider failed the check","No provider address answered over DoH with a valid certificate for every domain.","Pick another profile or try later."}},
   {"DNS_TIMEOUT_OR_UNREACHABLE_OR_TLS_FAILURE",new[]{"Provider not answering","The provider does not answer or its certificate failed the check.","Pick another profile or try later."}},
   {"NETWORK_PROBE_FAILED",new[]{"Network not checked","The network could not be checked.","Check the internet connection and try again."}},
   {"NO_ADDRESSES_FOR_PROFILE",new[]{"No addresses","The profile has no known IP addresses.","Press “Connect bridge”: the addresses will be found."}},
   {"BOOTSTRAP_FAILED",new[]{"Addresses not found","The provider's addresses could not be found: no connection or the name did not resolve. The bridge did not change.","Check the internet connection and try again."}},
   {"BOOTSTRAP_TIMEOUT",new[]{"Addresses not found in time","The address search took more than 20 seconds. The bridge did not change.","Try later or pick another profile."}},
   {"BOOTSTRAP_NO_ADDRESS",new[]{"Addresses failed the check","No address found passed the certificate and DoH check. The bridge did not change.","Pick another profile."}},
   {"DOH_TEMPLATE_INVALID",new[]{"Invalid DoH address","The DoH address must be an https:// link with a host name.","Fix the address in Settings."}},
   {"PROFILE_SWITCH_FAILED_PREVIOUS_RESTORED",new[]{"Change not applied","The new setup was not applied; the previous bridge is on again and checked.","Pick another profile or try again."}},
   {"APPLY_FAILED_ROLLED_BACK",new[]{"Connecting failed","All changes were undone.","Try again or pick another profile."}},
   {"DOH_ADD_VERIFICATION_FAILED",new[]{"Connecting failed","Windows did not confirm the added DoH entry.","Try again; if that fails, run “Diagnostics”."}},
   {"NRPT_UNSUPPORTED_BY_OS",new[]{"Windows not supported","This Windows version does not support NRPT rules.","Windows 10 or 11 x64 with updates is needed."}},
   {"DOH_UNSUPPORTED_BY_OS",new[]{"Windows not supported","This Windows version does not support DNS over HTTPS.","Update Windows to 10 (2004+) or 11."}},
   {"ADMIN_REQUIRED",new[]{"Administrator rights needed","Changing DNS needs administrator rights.","Confirm the Windows prompt (UAC)."}},
   {"UAC_CANCELLED",new[]{"Operation cancelled","The Windows prompt was declined; nothing changed.","Try again and confirm the prompt."}},
   {"TRUST_DECISION_REQUIRED",new[]{"Your decision is needed","Trust in the provider must be confirmed.","Press “Connect bridge” and confirm."}},
   {"OPERATION_IN_PROGRESS",new[]{"Already running","Another operation of the program is still running.","Wait a minute and try again."}},
   {"PROVIDER_SWITCH_OFFERED",new[]{"A working provider found","Another catalog profile passed the check for every domain. Nothing was changed.","Press “Connect via …” and confirm trust."}},
   {"NO_WORKING_PROVIDER",new[]{"No working provider","No catalog profile passed the check for every domain on this network. The bridge did not change.","Try later or add your own profile."}},
   {"HEALTH_OK",new[]{"Bridge is fine","The rules are in place, the provider answers, Windows returns its addresses.","Nothing to do."}},
   {"HEALTH_DRIFT_RULES",new[]{"Bridge changed","The program's rules or DoH entries changed or are not in effect.","Press “Repair” (one Windows prompt)."}},
   {"HEALTH_PROVIDER_DOWN",new[]{"Provider not answering","The provider's addresses do not answer over DoH.","Press “Repair”: addresses are found again or another provider is offered."}},
   {"HEALTH_RESOLVER_MISMATCH",new[]{"Windows answers differently","The rules are in place, but Windows does not answer with the provider's addresses.","Open Logs and press “Diagnostics”."}},
   {"HEALTH_NEEDS_MANUAL",new[]{"Manual check needed","The program's rule was edited or a foreign rule appeared.","Open “Details” and check the rule."}},
   {"HEALTH_UNKNOWN",new[]{"Check failed","The bridge state could not be checked. The bridge did not change.","The check will run again later."}},
   {"REPORT_SAVED",new[]{"Report saved","The file is saved on this computer and was not sent anywhere. User name, computer name, personal paths and network names are removed.","Check the file and send it yourself if you wish."}},
   {"REPORT_SAVE_FAILED",new[]{"Report not saved","The file could not be written.","Pick another folder and try again."}},
   {"ENGINE_TIMEOUT",new[]{"Windows did not answer","Windows did not answer in time: WMI or the DNS client may hang.","Try again or restart the WMI service."}},
   {"ELEVATION_TIMEOUT",new[]{"Taking too long","The operation takes too long and may still be running.","Wait a minute and check the state."}},
   {"ELEVATION_START_FAILED",new[]{"Rights not granted","Administrator rights could not be requested.","Start the program again."}},
   {"NO_MATCHING_OPERATION_RECEIPT",new[]{"Result unknown","The result of the operation is unknown.","Check the state or press “Diagnostics”."}},
   {"ENGINE_ERROR",new[]{"Service script failed","The service script ended with an error.","Open “Details” and the Logs tab."}},
   {"ENGINE_START_FAILED",new[]{"PowerShell did not start","Windows PowerShell could not be started.","Check that PowerShell is available and try again."}},
   {"ENGINE_TARGETS_MISSING",new[]{"Files damaged","The program's built-in configuration is damaged.","Download the program again."}},
   {"SCRIPT_TAMPERED",new[]{"Files changed","A service script was changed; the run was cancelled.","Restart the program; if that fails, download it again."}},
   {"SCRIPT_STORE_FAILED",new[]{"Files not prepared","The service files could not be prepared.","Free some disk space and try again."}},
   {"LIVE_INTEGRATION_FAILED",new[]{"Check failed","The check on this computer failed.","Open “Details” and the Logs tab."}},
   {"",new[]{"Unexpected answer","The program received an unknown answer.","Open “Details” and the Logs tab."}},
  };
 }
}
