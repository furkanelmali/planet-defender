using System.Collections.Generic;

namespace PlanetDefender
{
    // English is the default and the fallback for any key or language without a translation (GDD §13).
    // The player picks the language from the flag button on the main menu or in Settings; it is saved in pd_save_v1.
    public static class Loc
    {
        public static readonly string[] Languages = { "en", "tr", "ru", "es" };
        public static readonly string[] NativeNames = { "English", "Türkçe", "Русский", "Español" };
        public static string Language { get; private set; } = "en";
        public static int LanguageIndex => System.Array.IndexOf(Languages, Language);
        public static event System.Action Changed;

        public static void SetLanguage(string language)
        {
            string code = string.IsNullOrEmpty(language) ? "en" : language.ToLowerInvariant();
            if (code.Length > 2) code = code.Substring(0, 2);
            Language = System.Array.IndexOf(Languages, code) >= 0 ? code : "en";
            Changed?.Invoke();
        }

        public static string T(string key)
        {
            var table = Language == "tr" ? Tr : Language == "ru" ? Ru : Language == "es" ? Es : null;
            if (table != null && table.TryGetValue(key, out var text)) return text;
            return En.TryGetValue(key, out var en) ? en : key;
        }
        public static string F(string key, params object[] args) => string.Format(T(key), args);

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            {"title_a","PLANET"},{"title_b","DEFENDER"},{"loading","LOADING"},
            {"play","PLAY"},{"continue","CONTINUE RUN"},{"planets","PLANETS"},{"ranking","RANKING"},{"settings","SETTINGS"},
            {"best","BEST"},{"bank","BANK"},{"lives","{0} LIVES"},{"back","BACK"},
            {"music","MUSIC"},{"sfx","SOUND"},{"language","LANGUAGE"},{"on","ON"},{"off","OFF"},
            {"buy","BUY"},{"select","SELECT"},{"selected","SELECTED"},{"locked","LOCKED"},{"free","FREE"},
            {"level","LEVEL"},{"lv","LV {0}"},{"level_up","LEVEL UP"},{"skip","SKIP"},{"session_gold","session gold"},
            {"combo","x{0} COMBO"},{"pause","PAUSED"},{"resume","RESUME"},{"end_run","END RUN"},
            {"revive_title","PLANET DOWN"},{"revive_body","Watch an ad to continue with +1 life. One time per run."},
            {"watch_ad","WATCH AD: +1 LIFE"},{"no_thanks","END RUN"},
            {"game_over","GAME OVER"},{"new_best","NEW BEST!"},{"meteors_destroyed","Meteors destroyed"},{"reached_level","Reached level"},
            {"gold_earned","Gold earned"},{"gold_spent","Gold spent"},{"banked","BANKED"},{"retry","RETRY"},{"menu","MENU"},
            {"leaderboard","GLOBAL RANKING"},{"your_best","YOUR BEST"},{"lb_error","Could not load the ranking. Check your connection."},
            {"lb_loading","Loading ranking..."},{"lb_empty","No scores yet. Be the first!"},{"lb_unavailable","The global ranking appears when you play on Playgama. Your best score is saved here."},{"retry_small","TRY AGAIN"},{"rank_you","YOU"},
            {"hint_shoot","HOLD & DRAG TO SHOOT"},{"hint_shoot_body","Destroy meteors before they hit your planet."},
            {"hint_gold","LET THE GOLD THROUGH!"},{"hint_gold_body","Gold that reaches your planet is yours. Shooting it destroys it."},
            {"gold_lost","LOST"},{"shield_block","BLOCKED"},{"max","MAX"},{"new","NEW"},
            {"not_enough","Not enough gold"},{"countdown_go","GO!"},{"settle_note","Unspent session gold goes to your bank."},
            {"powers_note","Planets have different powers; all compete on one global board."},
            {"passive_none","Standard planet. No passive."},
            {"passive_fireRate","Blaster fires 8% faster."},
            {"passive_meteorSlow","Incoming meteors are 6% slower."},
            {"passive_xpBonus","+15% XP from destroyed meteors."},
            {"passive_damage","+15% bullet damage."},
            {"passive_shield","A shield blocks one meteor hit every 90 s."},
            {"passive_goldBonus","Gold that lands is worth 15% more."},
            {"passive_slowPulse","Every 40 s a pulse slows meteors 30% for 3 s."},
            {"planet_earth","EARTH"},{"planet_mercury","MERCURY"},{"planet_venus","VENUS"},{"planet_mars","MARS"},
            {"planet_jupiter","JUPITER"},{"planet_saturn","SATURN"},{"planet_uranus","URANUS"},{"planet_neptune","NEPTUNE"},
            {"up_rapid_fire","RAPID FIRE"},{"up_rapid_fire_d","Fire interval -15%."},
            {"up_double_shot","DOUBLE SHOT"},{"up_double_shot_d","Fire two bolts. Next tier tightens the spread."},
            {"up_piercing","PIERCING"},{"up_piercing_d","Bolts pierce +1 target."},
            {"up_damage_boost","DAMAGE BOOST"},{"up_damage_boost_d","Bolt damage +25%."},
            {"up_explosion","EXPLOSION"},{"up_explosion_d","Kills burst in a blast. Tier 2: wider."},
            {"up_freeze_pulse","FREEZE PULSE"},{"up_freeze_pulse_d","Every 20 s, slows meteors. Tier 2: longer."},
            {"up_orbital_drone","ORBITAL DRONE"},{"up_orbital_drone_d","A drone auto-targets meteors. Tier 2: two drones."},
            {"up_laser_beam","LASER BEAM"},{"up_laser_beam_d","A charged beam pierces every meteor. Tier 2: faster charge."},
            {"tier","Lv {0} → {1}"},
        };

        static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            {"loading","YÜKLENİYOR"},
            {"play","OYNA"},{"continue","OYUNA DEVAM"},{"planets","GEZEGENLER"},{"ranking","SIRALAMA"},{"settings","AYARLAR"},
            {"best","REKOR"},{"bank","BANKA"},{"lives","{0} CAN"},{"back","GERİ"},
            {"music","MÜZİK"},{"sfx","SES"},{"language","DİL"},{"on","AÇIK"},{"off","KAPALI"},
            {"buy","SATIN AL"},{"select","SEÇ"},{"selected","SEÇİLİ"},{"locked","KİLİTLİ"},{"free","ÜCRETSİZ"},
            {"level","SEVİYE"},{"lv","SV {0}"},{"level_up","SEVİYE ATLADIN"},{"skip","GEÇ"},{"session_gold","oturum altını"},
            {"combo","x{0} KOMBO"},{"pause","DURAKLATILDI"},{"resume","DEVAM"},{"end_run","OYUNU BİTİR"},
            {"revive_title","GEZEGEN DÜŞTÜ"},{"revive_body","Reklam izleyerek +1 canla devam et. Her oyunda bir kez."},
            {"watch_ad","REKLAM İZLE: +1 CAN"},{"no_thanks","OYUNU BİTİR"},
            {"game_over","OYUN BİTTİ"},{"new_best","YENİ REKOR!"},{"meteors_destroyed","Yok edilen meteor"},{"reached_level","Ulaşılan seviye"},
            {"gold_earned","Kazanılan altın"},{"gold_spent","Harcanan altın"},{"banked","BANKAYA"},{"retry","TEKRAR"},{"menu","MENÜ"},
            {"leaderboard","GLOBAL SIRALAMA"},{"your_best","SENİN REKORUN"},{"lb_error","Sıralama yüklenemedi. Bağlantını kontrol et."},
            {"lb_loading","Sıralama yükleniyor..."},{"lb_empty","Henüz skor yok. İlk sen ol!"},{"lb_unavailable","Global sıralama Playgama üzerinde oynarken görünür. Rekorun burada kayıtlı."},{"retry_small","TEKRAR DENE"},{"rank_you","SEN"},
            {"hint_shoot","BASILI TUT VE SÜRÜKLE"},{"hint_shoot_body","Meteorları gezegenine çarpmadan yok et."},
            {"hint_gold","ALTINI GEÇİR!"},{"hint_gold_body","Gezegene ulaşan altın senindir. Vurursan yok olur."},
            {"gold_lost","KAYIP"},{"shield_block","ENGELLENDİ"},{"max","MAKS"},{"new","YENİ"},
            {"not_enough","Yetersiz altın"},{"countdown_go","BAŞLA!"},{"settle_note","Harcanmayan oturum altını bankana eklenir."},
            {"powers_note","Gezegenlerin güçleri farklıdır; hepsi tek global tabloda yarışır."},
            {"passive_none","Standart gezegen. Pasif yok."},
            {"passive_fireRate","Blaster %8 daha hızlı ateş eder."},
            {"passive_meteorSlow","Gelen meteorlar %6 daha yavaş."},
            {"passive_xpBonus","Meteor yok etme XP'si +%15."},
            {"passive_damage","Mermi hasarı +%15."},
            {"passive_shield","Her 90 sn'de bir meteor darbesini engelleyen kalkan."},
            {"passive_goldBonus","Gezegene ulaşan altın %15 daha değerli."},
            {"passive_slowPulse","Her 40 sn'de meteorları 3 sn %30 yavaşlatan darbe."},
            {"planet_earth","DÜNYA"},{"planet_mercury","MERKÜR"},{"planet_venus","VENÜS"},{"planet_mars","MARS"},
            {"planet_jupiter","JÜPİTER"},{"planet_saturn","SATÜRN"},{"planet_uranus","URANÜS"},{"planet_neptune","NEPTÜN"},
            {"up_rapid_fire","HIZLI ATEŞ"},{"up_rapid_fire_d","Atış aralığı -%15."},
            {"up_double_shot","ÇİFT ATIŞ"},{"up_double_shot_d","İki mermi atar. Sonraki kademe açıyı daraltır."},
            {"up_piercing","DELİCİ"},{"up_piercing_d","Mermi +1 hedef deler."},
            {"up_damage_boost","HASAR ARTIŞI"},{"up_damage_boost_d","Mermi hasarı +%25."},
            {"up_explosion","PATLAMA"},{"up_explosion_d","Öldürmeler patlama yaratır. Kademe 2: daha geniş."},
            {"up_freeze_pulse","DONDURMA DARBESİ"},{"up_freeze_pulse_d","Her 20 sn'de meteorları yavaşlatır. Kademe 2: daha uzun."},
            {"up_orbital_drone","YÖRÜNGE DRONU"},{"up_orbital_drone_d","Meteorları otomatik hedefleyen dron. Kademe 2: iki dron."},
            {"up_laser_beam","LAZER IŞINI"},{"up_laser_beam_d","Şarjlı ışın tüm meteorları deler. Kademe 2: hızlı şarj."},
            {"tier","Sv {0} → {1}"},
        };
        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            {"loading","ЗАГРУЗКА"},
            {"play","ИГРАТЬ"},{"continue","ПРОДОЛЖИТЬ"},{"planets","ПЛАНЕТЫ"},{"ranking","РЕЙТИНГ"},{"settings","НАСТРОЙКИ"},
            {"best","РЕКОРД"},{"bank","БАНК"},{"lives","ЖИЗНИ: {0}"},{"back","НАЗАД"},
            {"music","МУЗЫКА"},{"sfx","ЗВУК"},{"language","ЯЗЫК"},{"on","ВКЛ"},{"off","ВЫКЛ"},
            {"buy","КУПИТЬ"},{"select","ВЫБРАТЬ"},{"selected","ВЫБРАНО"},{"locked","ЗАКРЫТО"},{"free","БЕСПЛАТНО"},
            {"level","УРОВЕНЬ"},{"lv","УР {0}"},{"level_up","НОВЫЙ УРОВЕНЬ"},{"skip","ПРОПУСТИТЬ"},{"session_gold","золото забега"},
            {"combo","x{0} КОМБО"},{"pause","ПАУЗА"},{"resume","ПРОДОЛЖИТЬ"},{"end_run","ЗАВЕРШИТЬ"},
            {"revive_title","ПЛАНЕТА ПАЛА"},{"revive_body","Посмотрите рекламу и продолжите с +1 жизнью. Один раз за забег."},
            {"watch_ad","РЕКЛАМА: +1 ЖИЗНЬ"},{"no_thanks","ЗАВЕРШИТЬ"},
            {"game_over","ИГРА ОКОНЧЕНА"},{"new_best","НОВЫЙ РЕКОРД!"},{"meteors_destroyed","Сбито метеоров"},{"reached_level","Уровень"},
            {"gold_earned","Получено золота"},{"gold_spent","Потрачено золота"},{"banked","В БАНК"},{"retry","ЕЩЁ РАЗ"},{"menu","МЕНЮ"},
            {"leaderboard","МИРОВОЙ РЕЙТИНГ"},{"your_best","ВАШ РЕКОРД"},{"lb_error","Не удалось загрузить рейтинг. Проверьте подключение."},
            {"lb_loading","Загрузка рейтинга..."},{"lb_empty","Пока нет результатов. Будьте первым!"},{"lb_unavailable","Мировой рейтинг доступен при игре на Playgama. Ваш рекорд сохранён здесь."},{"retry_small","ПОВТОРИТЬ"},{"rank_you","ВЫ"},
            {"hint_shoot","ЗАЖМИТЕ И ВЕДИТЕ, ЧТОБЫ СТРЕЛЯТЬ"},{"hint_shoot_body","Уничтожайте метеоры, пока они не ударили по планете."},
            {"hint_gold","ПРОПУСТИТЕ ЗОЛОТО!"},{"hint_gold_body","Золото, долетевшее до планеты, ваше. Выстрел его уничтожит."},
            {"gold_lost","ПОТЕРЯНО"},{"shield_block","БЛОК"},{"max","МАКС"},{"new","НОВОЕ"},
            {"not_enough","Недостаточно золота"},{"countdown_go","ВПЕРЁД!"},{"settle_note","Непотраченное золото забега уйдёт в банк."},
            {"powers_note","У планет разные способности; все соревнуются в одном мировом рейтинге."},
            {"passive_none","Обычная планета. Без пассивки."},
            {"passive_fireRate","Бластер стреляет на 8% быстрее."},
            {"passive_meteorSlow","Метеоры летят на 6% медленнее."},
            {"passive_xpBonus","+15% опыта за метеоры."},
            {"passive_damage","+15% урона снарядов."},
            {"passive_shield","Щит блокирует один удар метеора каждые 90 с."},
            {"passive_goldBonus","Долетевшее золото дороже на 15%."},
            {"passive_slowPulse","Каждые 40 с импульс замедляет метеоры на 30% на 3 с."},
            {"planet_earth","ЗЕМЛЯ"},{"planet_mercury","МЕРКУРИЙ"},{"planet_venus","ВЕНЕРА"},{"planet_mars","МАРС"},
            {"planet_jupiter","ЮПИТЕР"},{"planet_saturn","САТУРН"},{"planet_uranus","УРАН"},{"planet_neptune","НЕПТУН"},
            {"up_rapid_fire","СКОРОСТРЕЛ"},{"up_rapid_fire_d","Интервал выстрела -15%."},
            {"up_double_shot","ДВОЙНОЙ ВЫСТРЕЛ"},{"up_double_shot_d","Два снаряда. Следующий уровень сужает разброс."},
            {"up_piercing","ПРОБИТИЕ"},{"up_piercing_d","Снаряд пробивает +1 цель."},
            {"up_damage_boost","УСИЛЕНИЕ УРОНА"},{"up_damage_boost_d","Урон снарядов +25%."},
            {"up_explosion","ВЗРЫВ"},{"up_explosion_d","Убийства вызывают взрыв. Ур. 2: шире."},
            {"up_freeze_pulse","ЛЕДЯНОЙ ИМПУЛЬС"},{"up_freeze_pulse_d","Каждые 20 с замедляет метеоры. Ур. 2: дольше."},
            {"up_orbital_drone","ОРБИТАЛЬНЫЙ ДРОН"},{"up_orbital_drone_d","Дрон сам стреляет по метеорам. Ур. 2: два дрона."},
            {"up_laser_beam","ЛАЗЕРНЫЙ ЛУЧ"},{"up_laser_beam_d","Заряженный луч пробивает все метеоры. Ур. 2: быстрее заряд."},
            {"tier","Ур {0} → {1}"},
        };

        static readonly Dictionary<string, string> Es = new Dictionary<string, string>
        {
            {"loading","CARGANDO"},
            {"play","JUGAR"},{"continue","CONTINUAR PARTIDA"},{"planets","PLANETAS"},{"ranking","CLASIFICACIÓN"},{"settings","AJUSTES"},
            {"best","RÉCORD"},{"bank","BANCO"},{"lives","{0} VIDAS"},{"back","VOLVER"},
            {"music","MÚSICA"},{"sfx","SONIDO"},{"language","IDIOMA"},{"on","SÍ"},{"off","NO"},
            {"buy","COMPRAR"},{"select","ELEGIR"},{"selected","ELEGIDO"},{"locked","BLOQUEADO"},{"free","GRATIS"},
            {"level","NIVEL"},{"lv","NV {0}"},{"level_up","¡SUBES DE NIVEL!"},{"skip","SALTAR"},{"session_gold","oro de la partida"},
            {"combo","x{0} COMBO"},{"pause","PAUSA"},{"resume","CONTINUAR"},{"end_run","TERMINAR"},
            {"revive_title","PLANETA CAÍDO"},{"revive_body","Mira un anuncio para seguir con +1 vida. Una vez por partida."},
            {"watch_ad","ANUNCIO: +1 VIDA"},{"no_thanks","TERMINAR"},
            {"game_over","FIN DE LA PARTIDA"},{"new_best","¡NUEVO RÉCORD!"},{"meteors_destroyed","Meteoros destruidos"},{"reached_level","Nivel alcanzado"},
            {"gold_earned","Oro ganado"},{"gold_spent","Oro gastado"},{"banked","AL BANCO"},{"retry","REINTENTAR"},{"menu","MENÚ"},
            {"leaderboard","CLASIFICACIÓN MUNDIAL"},{"your_best","TU RÉCORD"},{"lb_error","No se pudo cargar la clasificación. Revisa tu conexión."},
            {"lb_loading","Cargando clasificación..."},{"lb_empty","Aún no hay puntuaciones. ¡Sé el primero!"},{"lb_unavailable","La clasificación mundial aparece al jugar en Playgama. Tu récord se guarda aquí."},{"retry_small","REINTENTAR"},{"rank_you","TÚ"},
            {"hint_shoot","MANTÉN Y ARRASTRA PARA DISPARAR"},{"hint_shoot_body","Destruye los meteoros antes de que golpeen tu planeta."},
            {"hint_gold","¡DEJA PASAR EL ORO!"},{"hint_gold_body","El oro que llega a tu planeta es tuyo. Si le disparas, se destruye."},
            {"gold_lost","PERDIDO"},{"shield_block","BLOQUEADO"},{"max","MÁX"},{"new","NUEVO"},
            {"not_enough","Oro insuficiente"},{"countdown_go","¡YA!"},{"settle_note","El oro no gastado de la partida va a tu banco."},
            {"powers_note","Cada planeta tiene poderes distintos; todos compiten en una clasificación mundial."},
            {"passive_none","Planeta estándar. Sin pasiva."},
            {"passive_fireRate","El bláster dispara un 8% más rápido."},
            {"passive_meteorSlow","Los meteoros llegan un 6% más lentos."},
            {"passive_xpBonus","+15% de XP por meteoros destruidos."},
            {"passive_damage","+15% de daño de los proyectiles."},
            {"passive_shield","Un escudo bloquea un impacto cada 90 s."},
            {"passive_goldBonus","El oro que llega vale un 15% más."},
            {"passive_slowPulse","Cada 40 s un pulso frena los meteoros un 30% durante 3 s."},
            {"planet_earth","TIERRA"},{"planet_mercury","MERCURIO"},{"planet_venus","VENUS"},{"planet_mars","MARTE"},
            {"planet_jupiter","JÚPITER"},{"planet_saturn","SATURNO"},{"planet_uranus","URANO"},{"planet_neptune","NEPTUNO"},
            {"up_rapid_fire","FUEGO RÁPIDO"},{"up_rapid_fire_d","Intervalo de disparo -15%."},
            {"up_double_shot","DISPARO DOBLE"},{"up_double_shot_d","Dispara dos proyectiles. El nivel 2 cierra el ángulo."},
            {"up_piercing","PERFORANTE"},{"up_piercing_d","Los proyectiles atraviesan +1 objetivo."},
            {"up_damage_boost","MÁS DAÑO"},{"up_damage_boost_d","Daño de los proyectiles +25%."},
            {"up_explosion","EXPLOSIÓN"},{"up_explosion_d","Las bajas estallan en área. Nivel 2: más amplia."},
            {"up_freeze_pulse","PULSO HELADO"},{"up_freeze_pulse_d","Cada 20 s frena los meteoros. Nivel 2: dura más."},
            {"up_orbital_drone","DRON ORBITAL"},{"up_orbital_drone_d","Un dron dispara solo a los meteoros. Nivel 2: dos drones."},
            {"up_laser_beam","RAYO LÁSER"},{"up_laser_beam_d","Un rayo cargado atraviesa todos los meteoros. Nivel 2: carga más rápida."},
            {"tier","Nv {0} → {1}"},
        };
    }
}
