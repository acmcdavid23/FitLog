/**
 * Session detail / static list: load next-exercise AI suggestion (same rules as active workout).
 * opts: { sessionName, exerciseLibrary, musclesHitTodayServer, loggedExerciseNames, ids: { card, loading, suggestionBody, name, reason, empty } }
 */
window.fitlogInitWorkoutNextRec = function (opts) {
    const sessionName = opts.sessionName || '';
    const exerciseLibrary = Array.isArray(opts.exerciseLibrary) ? opts.exerciseLibrary : [];
    const musclesHitTodayServer = Array.isArray(opts.musclesHitTodayServer) ? opts.musclesHitTodayServer : [];
    const logged = (opts.loggedExerciseNames || []).map(function (n) { return (n || '').trim(); }).filter(Boolean);
    const ids = opts.ids || {};
    const el = function (k) { return document.getElementById(ids[k]); };

    const PENDING = '__fitlog_pending_exercise__';
    function isPending(n) { return (n || '').trim() === PENDING; }

    function lookupExerciseMeta(name) {
        const n = (name || '').trim().toLowerCase();
        const ex = exerciseLibrary.find(function (e) { return (e.name || '').toLowerCase() === n; });
        if (!ex) return { muscleGroup: 'General', description: '', tips: '' };
        return { muscleGroup: ex.muscleGroup || 'General', description: ex.description || '', tips: ex.tips || '' };
    }

    function shuffleInPlace(arr) {
        for (var i = arr.length - 1; i > 0; i--) {
            var j = Math.floor(Math.random() * (i + 1));
            var t = arr[i]; arr[i] = arr[j]; arr[j] = t;
        }
        return arr;
    }

    var muscleAliases = {
        chest: ['chest', 'pec'], back: ['back', 'lats', 'rhomb', 'trap '], shoulders: ['shoulder', 'delts', 'delt'],
        quads: ['quad', 'quadriceps'], hamstrings: ['hamstring'], glutes: ['glute'], biceps: ['bicep'], triceps: ['tricep'],
        calves: ['calf'], core: ['core', 'abs', 'ab', 'oblique']
    };

    function canonicalMuscle(m) {
        var val = (m || '').toLowerCase();
        for (var k in muscleAliases) {
            if (muscleAliases[k].some(function (t) { return val.includes(t); })) return k;
        }
        return '';
    }

    function inferSessionFocusMode() {
        var raw = sessionName || '';
        var sn = raw.toLowerCase();
        var st = sn.trim();
        if (st === 'chest' || st === 'pec' || st === 'pecs') return 'chest';
        if (st === 'back' || st === 'pull') return 'back';
        if (st === 'legs' || st === 'leg') return 'legs';
        if (st === 'shoulders' || st === 'shoulder' || st === 'delts') return 'shoulders';
        if (st === 'arms') return 'arms';
        if (st === 'core' || st === 'abs') return 'core';
        try {
            if (/\bchest\b|\bpecs?\b/i.test(raw)) return 'chest';
            if (/\bshoulders?\b|\bdelts?\b/i.test(raw)) return 'shoulders';
            if (/\barms?\b|\bbiceps?\b|\btriceps?\b/i.test(raw)) return 'arms';
            if (/\blegs?\b|\bquad\b|\bglutes?\b|\bhamstrings?\b/i.test(raw)) return 'legs';
            if (/\bback\b|\blats?\b/i.test(raw)) return 'back';
            if (/\bpull\s*day\b/i.test(raw)) return 'back';
            if (/\bcore\b|\babs\b/i.test(raw)) return 'core';
            if (/\bcardio\b|\bhiit\b|\bconditioning\b/i.test(raw)) return 'cardio';
        } catch (_) { }
        var rules = [
            { mode: 'chest', keys: ['chest day', 'chest &', 'chest,', 'chest -', 'pec day', 'pec ', ' pec', 'upper chest', 'lower chest'] },
            { mode: 'back', keys: ['back day', 'back ', 'pull day', 'pull ', 'lat day', 'lat ', 'row day'] },
            { mode: 'legs', keys: ['leg day', 'leg ', 'legs', 'quad day', 'squat day', 'lower body', 'glute day', 'hamstring'] },
            { mode: 'shoulders', keys: ['shoulder day', 'shoulder ', 'delt day', ' delt', 'ohp', 'overhead'] },
            { mode: 'arms', keys: ['arm day', 'arms', 'bicep', 'tricep', 'bi/tri', 'bis and tris'] },
            { mode: 'core', keys: ['core day', 'core ', 'ab day', 'abs day', ' ab '] },
            { mode: 'cardio', keys: ['cardio', 'hiit', 'conditioning', 'endurance'] }
        ];
        for (var i = 0; i < rules.length; i++) {
            var r = rules[i];
            if (r.keys.some(function (k) { return sn.includes(k); })) return r.mode;
        }
        return '';
    }

    function voteFocusModeFromLogged(loggedNames) {
        var counts = {};
        for (var i = 0; i < loggedNames.length; i++) {
            var n = (loggedNames[i] || '').trim();
            if (!n || isPending(n)) continue;
            var mg = lookupExerciseMeta(n).muscleGroup || '';
            var c = canonicalMuscle(mg);
            if (['quads', 'hamstrings', 'glutes', 'calves'].indexOf(c) >= 0) c = 'legs';
            if (c) counts[c] = (counts[c] || 0) + 1;
        }
        var keys = Object.keys(counts);
        if (!keys.length) return '';
        var top = keys.map(function (k) { return [k, counts[k]]; }).sort(function (a, b) { return b[1] - a[1]; })[0];
        return top ? top[0] : '';
    }

    function exerciseMatchesFocusMode(ex, mode) {
        if (!mode || mode === 'cardio') return true;
        var c = canonicalMuscle(ex.muscleGroup || '');
        if (mode === 'legs') return ['quads', 'hamstrings', 'glutes', 'calves'].indexOf(c) >= 0;
        if (mode === 'arms') return c === 'biceps' || c === 'triceps';
        return c === mode;
    }

    function focusModeLabel(mode) {
        var map = { chest: 'chest / pectorals', back: 'back (lats, mid-back, traps as applicable)', legs: 'legs (quads, hamstrings, glutes, calves)', shoulders: 'shoulders / deltoids', arms: 'arms (biceps and/or triceps)', core: 'core / trunk', cardio: 'conditioning' };
        return map[mode] || mode || 'balanced';
    }

    function buildReasonFallback(ex, focusMode) {
        var mg = ex.muscleGroup || '';
        if (focusMode === 'chest') return 'Hits ' + (mg || 'chest') + ' pressing or fly work.';
        if (focusMode === 'back') return 'Pulling pattern for ' + (mg || 'back') + '.';
        if (focusMode === 'legs') return 'Lower-body emphasis through ' + (mg || 'legs') + '.';
        if (focusMode === 'shoulders') return 'Shoulder-focused (' + (mg || 'delts') + ').';
        if (focusMode === 'arms') return 'Arm isolation for ' + (mg || 'biceps/triceps') + '.';
        if (focusMode === 'core') return 'Trunk / core work via ' + (mg || 'core') + '.';
        return 'Primary stimulus: ' + (mg || canonicalMuscle(mg) || 'target muscle') + '.';
    }

    function showEmpty(msg) {
        var card = el('card'); var load = el('loading'); var body = el('suggestionBody'); var empty = el('empty');
        if (load) load.style.display = 'none';
        if (body) { body.style.display = 'none'; }
        if (empty) { empty.textContent = msg; empty.style.display = 'block'; }
        if (card) card.style.display = 'block';
    }

    function showSuggestion(name, reason) {
        var card = el('card'); var load = el('loading'); var body = el('suggestionBody'); var empty = el('empty');
        var ne = el('name'); var re = el('reason');
        if (load) load.style.display = 'none';
        if (empty) { empty.style.display = 'none'; empty.textContent = ''; }
        if (ne) ne.textContent = name;
        if (re) re.textContent = reason;
        if (body) body.style.display = 'block';
        if (card) card.style.display = 'block';
        window.fitlogSessionPendingRec = { name: name, reason: reason };
    }

    function hideAll() {
        var card = el('card');
        if (card) card.style.display = 'none';
        window.fitlogSessionPendingRec = null;
    }

    var recentAISuggestionKeys = [];

    async function run() {
        var card = el('card');
        if (!card) return;
        try {
            var loggedFiltered = logged.filter(function (n) { return !isPending(n); });
            var loggedSet = new Set(loggedFiltered.map(function (v) { return (v || '').trim().toLowerCase(); }));
            var fromServer = musclesHitTodayServer;
            var hitsNormalized = fromServer.map(function (m) { return (m || '').trim().toLowerCase(); }).filter(Boolean).filter(function (v, i, a) { return a.indexOf(v) === i; });
            var hitCanon = new Set(hitsNormalized.map(canonicalMuscle).filter(Boolean));

            function buildPool(excludeRecent) {
                var pool = exerciseLibrary.filter(function (ex) {
                    var n = (ex.name || '').trim().toLowerCase();
                    if (!n || loggedSet.has(n)) return false;
                    return true;
                });
                if (excludeRecent && pool.length > 1) {
                    var recentSet = new Set(recentAISuggestionKeys);
                    var narrowed = pool.filter(function (ex) { return !recentSet.has((ex.name || '').trim().toLowerCase()); });
                    if (narrowed.length) pool = narrowed;
                }
                return pool;
            }

            var basePool = buildPool(true);
            if (!basePool.length) basePool = buildPool(false);
            if (!basePool.length) { showEmpty('No more suggestions — add exercises to your library or log more lifts.'); return; }

            var focusMode = inferSessionFocusMode() || voteFocusModeFromLogged(loggedFiltered);
            var strictFocus = !!(focusMode && focusMode !== 'cardio');
            var focusPool = focusMode ? basePool.filter(function (ex) { return exerciseMatchesFocusMode(ex, focusMode); }) : basePool;

            if (strictFocus && !focusPool.length) { showEmpty('No more suggestions for this workout focus.'); return; }

            var preferred = focusPool.filter(function (ex) { return !hitCanon.has(canonicalMuscle(ex.muscleGroup || '')); });
            if (!preferred.length) preferred = focusPool;

            var options = preferred.slice(0, 48);
            if (!options.length) options = focusPool.slice(0, 48);
            shuffleInPlace(options);
            var optionNames = options.map(function (o) { return o.name; }).filter(Boolean);
            if (!optionNames.length) { showEmpty('No more suggestions right now.'); return; }

            var focusLabel = focusModeLabel(focusMode);
            var chosen = options[0];
            var reasonText = buildReasonFallback(chosen, focusMode);
            var aiPicked = false;

            try {
                var strict = strictFocus
                    ? 'STRICT: This workout\'s focus is **' + focusLabel + '**. ONLY pick an exercise whose PRIMARY muscle group matches that focus.'
                    : 'Pick a sensible next exercise. Reason must match the lift\'s PRIMARY muscles.';
                var res = await fetch('/AICoach/Chat', {
                    method: 'POST', headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        message: strict + ' Session name: "' + (sessionName || 'Workout') + '". Already logged: ' + (loggedFiltered.join(', ') || 'none') + '. Muscles trained earlier today: ' + (fromServer.join(', ') || 'none') + '. Allowed exercises (choose EXACTLY one): ' + optionNames.join(', ') + '. Reply ONLY as: Name: [exact name from list] | Reason: [one accurate sentence] | Muscle: [primary muscle group]',
                        history: []
                    })
                });
                if (!res.ok) throw new Error('http');
                var data = await res.json();
                var text = typeof data.response === 'string' ? data.response.trim() : '';
                var nameMatch = text.match(/Name:\s*([^|]+)/i);
                var reasonMatch = text.match(/Reason:\s*([^|]+)/i);
                var aiNameLower = (nameMatch ? nameMatch[1] : '').replace(/\*+/g, '').replace(/[`"'[\]]/g, '').trim().replace(/^the\s+/i, '').replace(/\.+$/g, '').trim().toLowerCase();
                var fromAi = options.find(function (o) { return (o.name || '').toLowerCase() === aiNameLower; });
                if (fromAi && strictFocus && !exerciseMatchesFocusMode(fromAi, focusMode)) fromAi = null;
                if (fromAi) {
                    chosen = fromAi;
                    var r = reasonMatch ? reasonMatch[1].trim() : '';
                    reasonText = r && r.length > 12 ? r : buildReasonFallback(chosen, focusMode);
                    aiPicked = true;
                }
            } catch (_) { }

            if (strictFocus && !aiPicked) { showEmpty('No AI suggestion available — try again in a moment.'); return; }
            if (strictFocus && !exerciseMatchesFocusMode(chosen, focusMode)) { showEmpty('No suggestion matched this workout focus.'); return; }

            recentAISuggestionKeys.push((chosen.name || '').trim().toLowerCase());
            if (recentAISuggestionKeys.length > 12) recentAISuggestionKeys.shift();

            showSuggestion(chosen.name, reasonText);
        } catch (e) {
            hideAll();
        }
    }

    run();
};
