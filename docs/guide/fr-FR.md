# Guide AirspyPanel 3.0

[English](en-US.md) · [简体中文](zh-CN.md) · [日本語](ja-JP.md) · [Français](fr-FR.md) · [Deutsch](de-DE.md) · [Русский](ru-RU.md)

## Installation et mise à jour

AirspyPanel est un panneau Windows communautaire pour la réception ADS-B Airspy et Virtual Radar Server (VRS), sans affiliation officielle. Windows 10/11 et .NET Framework 4.8 sont requis. Le développement est testé sous Windows 11 x64 ; les tests automatiques ne certifient pas les autres systèmes ni le matériel radio.

Téléchargez l’installateur EXE ou le ZIP portable depuis les [versions](https://github.com/PriscillaOvO/AirspyPanel/releases). Vérifiez le SHA-256 avec `Get-FileHash` et `SHA256SUMS.txt`. Les fichiers ne sont pas signés : ne désactivez pas votre protection. L’installation concerne l’utilisateur courant, propose un raccourci facultatif et n’ajoute pas de démarrage automatique. Quittez le panneau avant une mise à jour. Les réglages sont conservés lors des mises à jour et, par défaut, de la désinstallation ; leur suppression exige un choix explicite. La désinstallation silencieuse les conserve.

Pour le ZIP, extrayez tout et lancez `AirspyPanel.exe` avec son `.config` à côté. Les deux éditions utilisent `%LOCALAPPDATA%\AirspyPanel`. Ports, gain et chemins existants sont conservés. Les anciens réglages chinois restent en chinois ; une nouvelle installation suit la langue système, avec repli sur l’anglais.

## Première utilisation

Installez séparément [ADS-B SPY Windows](https://airspy.com/download/) et [VRS](https://www.virtualradarserver.co.uk/Download.aspx), en conservant les DLL du décodeur. Aucun logiciel tiers ni pilote n’est inclus. VRS peut nécessiter la fonctionnalité Windows .NET Framework 3.5.

Dans Réglages, détectez les programmes ou sélectionnez `airspy_adsb.exe` et `VirtualRadar.exe` dans Programmes et carte. La recherche examine les emplacements habituels, peut être annulée et ignore les dossiers inaccessibles ou liens de dossiers. Elle ne parcourt pas tous les disques, ne lance rien et ne modifie pas VRS. Choisissez un dossier plus précis au besoin. Un chemin valide est conservé ; plusieurs résultats exigent une sélection. Enregistrez explicitement.

Branchez un Airspy R0/R2/Mini avec antenne 1090 MHz et fermez les applications utilisant le même appareil. Démarrez la réception, configurez et lancez VRS, puis ouvrez la carte. VRS local déjà actif est fermé normalement, sa configuration sauvegardée puis mise à jour, et VRS redémarre sans seconde confirmation. Une fermeture impossible est signalée, jamais forcée.

## Carte, état et mesures

URL par défaut : `http://127.0.0.1/VirtualRadar/`. `/VirtualRadar/desktop.html#` est aussi accepté ; le panneau en déduit `AircraftList.json`. Adaptez le port Web dans l’URL. Le décodeur et VRS doivent partager le port TCP de données (30005 par défaut) et le format Beast. Le port Web est distinct.

Gris : non vérifié ; vert : API, source et configuration conformes ; jaune : API disponible mais source/configuration à vérifier ; rouge : indisponible. Cliquez sur l’état pour les détails et l’heure. Zéro avion est valide. Une panne de fond de carte n’est pas une panne de l’API locale. Choisissez le récepteur dans les réglages en cas de plusieurs correspondances ; les noms personnalisés sont acceptés. Une configuration locale différente ne bloque pas la carte.

Les niveaux moyen et maximal proviennent de trois secondes de messages Beast, convertis de 0–255 à 0–100 %. Ce ne sont ni des dBm calibrés, ni un spectre, ni le bruit de fond. Les avions sont dédupliqués par ICAO explicite DF11/17 sur 60 secondes. Les règles de VRS peuvent différer.

## Utilisation et préférences

La fenêtre complète est redimensionnable ; le mode compact est fixe, environ 320×620 DIP, limité à l’espace de travail de l’écran. Réglages, journal et détails restent dans cette fenêtre. Changer de mode ne redémarre pas la réception. Les thèmes clair/sombre/système et six langues sont disponibles. La langue change immédiatement sans effacer réception ou saisies ; les sorties tierces et détails d’erreurs système restent d’origine.

Tout arrêter stoppe le décodeur lancé par le panneau et ferme VRS local, même lancé manuellement. L’onglet de carte reste ouvert. Quitter utilise la même séquence. Si VRS refuse de fermer : réessayez, annulez, ou choisissez explicitement de quitter seulement le panneau. Réduire dans la zone de notification continue la réception. Le premier choix de fermeture peut être mémorisé et modifié dans Apparence et fermeture. Un double-clic sur l’icône restaure la page ; un second lancement rappelle l’instance existante.

Le journal se trouve dans Réglages. Les événements clés sont prioritaires, alertes et erreurs mises en évidence, répétitions regroupées. La sortie brute est séparée. Remonter suspend le suivi ; Derniers le reprend. L’export respecte la vue et le niveau choisis. Chaque vue garde 500 entrées en mémoire, sans enregistrement automatique des journaux ordinaires.

Paramètres avancés : gain auto ou 0–21, un excès peut saturer. Les 12/20/24 MHz sont l’échantillonnage, pas les 1090 MHz de réception ni du MLAT autonome ; 24 MHz exige un R2 compatible. Filtre 1–60, défaut 4, plus élevé peut consommer plus de CPU. Le compactage USB `-p` concerne le transport, pas l’alimentation Bias-Tee. Les valeurs recommandées ne modifient ni chemins ni URL.

## Dépannage, développement et licence

Sans messages, vérifiez occupation du matériel, antenne, USB, pilote et conditions de réception. Pour VRS, contrôlez adresse, port Web et service ; les détails distinguent délai dépassé, mauvaise réponse HTML/JSON et source différente. Une page AdGuard indique des règles de filtrage local à vérifier. Zéro avion ne suffit pas à conclure à une panne.

Les sauvegardes VRS sont près de sa configuration dans `%LOCALAPPDATA%\VirtualRadar`. Quittez VRS avant une restauration manuelle. Pour signaler un problème, indiquez version, Windows, langue et étapes ; retirez chemins privés, localisation, données reçues et identifiants avant de partager. Consultez le relevé de vérification pour les limites réelles.

Compilez avec `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1`. Le résultat est dans `bin`. Lancez `bin\AirspyPanel.exe --self-test`, `--render` ou `--render-languages`. Les prérequis de l’installateur sont dans CONTRIBUTING.md.

Chaîne : Airspy → ADS-B SPY → Beast TCP → panneau et VRS → carte du navigateur. Le panneau contrôle et observe, sans remplacer décodeur ou serveur de carte. Le code et la documentation propres sont sous MIT ; les produits tiers gardent leurs licences. Aucun droit de redistribution d’ADS-B SPY n’est présumé.
