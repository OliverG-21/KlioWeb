(function () {
    var lb       = document.getElementById('lightbox');
    var lbImg    = document.getElementById('lightbox-img');
    var lbDate   = document.getElementById('lb-date');
    var lbCamera = document.getElementById('lb-camera');
    var lbLoc    = document.getElementById('lb-location');
    var lbBack   = document.getElementById('lightbox-back');
    var lbMap    = document.getElementById('lightbox-map');
    var lbPrev   = document.getElementById('lightbox-prev');
    var lbNext   = document.getElementById('lightbox-next');
    var geoCache = {};

    // Navigation state
    var _navItems = [];
    var _navIndex = -1;

    function buildNav(imageId) {
        var els = Array.from(document.querySelectorAll('.gallery-item'));
        _navItems = els.map(function (el) {
            return {
                id:     el.dataset.id,
                date:   el.dataset.date   || '',
                camera: el.dataset.camera || '',
                lat:    el.dataset.lat    || null,
                lon:    el.dataset.lon    || null
            };
        });
        _navIndex = _navItems.findIndex(function (it) {
            return String(it.id) === String(imageId);
        });
    }

    function updateNavButtons() {
        lbPrev.style.display = (_navIndex > 0)                        ? 'flex' : 'none';
        lbNext.style.display = (_navIndex < _navItems.length - 1)     ? 'flex' : 'none';
    }

    // Detect if opened from map — only valid for the specific image linked from the map
    var urlParams      = new URLSearchParams(location.search);
    var fromMap        = urlParams.get('fromMap') === '1';
    var fromMapImageId = fromMap ? urlParams.get('openImage') : null;
    var mapBackUrl     = null;
    if (fromMap) {
        var imgLat  = urlParams.get('imgLat');
        var imgLon  = urlParams.get('imgLon');
        var mapZoom = urlParams.get('mapZoom') || '13';
        mapBackUrl  = '/map?lat=' + imgLat + '&lon=' + imgLon + '&zoom=' + mapZoom;
    }

    function resolveLocation(lat, lon) {
        var key = lat + ',' + lon;
        if (geoCache[key] !== undefined) return Promise.resolve(geoCache[key]);
        geoCache[key] = '';
        return fetch(
            'https://nominatim.openstreetmap.org/reverse?format=json&lat=' + lat + '&lon=' + lon,
            { headers: { 'Accept-Language': 'de' } }
        ).then(function (r) { return r.json(); })
         .then(function (j) {
            var a = j.address || {};
            var city = a.city || a.town || a.village || a.municipality || '';
            var country = a.country || '';
            geoCache[key] = [city, country].filter(Boolean).join(', ');
            return geoCache[key];
         }).catch(function () { return ''; });
    }

    window.openLightbox = function (imageId, date, camera, lat, lon) {
        buildNav(imageId);
        updateNavButtons();

        lbImg.src            = '/images/serve/' + imageId;
        lbDate.textContent   = date || '';
        lbCamera.textContent = camera ? '📷 ' + camera : '';
        lbLoc.textContent    = '';
        lb.style.display     = 'flex';

        var hasGps    = lat != null && lon != null && !isNaN(parseFloat(lat)) && !isNaN(parseFloat(lon));
        var isFromMap = mapBackUrl && fromMapImageId && String(imageId) === String(fromMapImageId);

        if (isFromMap) {
            lbBack.href          = mapBackUrl;
            lbBack.style.display = 'flex';
            lbMap.style.display  = 'none';
        } else {
            lbBack.style.display = 'none';
            if (hasGps) {
                lbMap.href          = '/map?lat=' + parseFloat(lat) + '&lon=' + parseFloat(lon) + '&zoom=13';
                lbMap.style.display = 'flex';
            } else {
                lbMap.style.display = 'none';
            }
        }

        if (hasGps) {
            lbLoc.textContent = '📍 …';
            resolveLocation(parseFloat(lat), parseFloat(lon)).then(function (loc) {
                lbLoc.textContent = loc ? '📍 ' + loc : '';
            });
        }
    };

    window.closeLightbox = function () {
        lb.style.display = 'none';
        lbImg.src = '';
        lbPrev.style.display = 'none';
        lbNext.style.display = 'none';
        _navItems = [];
        _navIndex = -1;
    };

    window.lightboxNavigate = function (delta) {
        if (lb.style.display === 'none') return;
        var newIndex = _navIndex + delta;
        if (newIndex < 0 || newIndex >= _navItems.length) return;
        var item = _navItems[newIndex];
        _navIndex = newIndex;
        window.openLightbox(item.id, item.date, item.camera,
            item.lat || null, item.lon || null);
    };

    document.getElementById('lightbox-close').addEventListener('click', window.closeLightbox);
    lb.addEventListener('click', function (e) {
        var inner = document.getElementById('lightbox-inner');
        if (!inner.contains(e.target)
            && e.target !== lbBack  && !lbBack.contains(e.target)
            && e.target !== lbMap   && !lbMap.contains(e.target)
            && e.target !== lbPrev  && !lbPrev.contains(e.target)
            && e.target !== lbNext  && !lbNext.contains(e.target)) {
            window.closeLightbox();
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape')     { window.closeLightbox(); }
        if (e.key === 'ArrowLeft')  { window.lightboxNavigate(-1); e.preventDefault(); }
        if (e.key === 'ArrowRight') { window.lightboxNavigate(1);  e.preventDefault(); }
    });
})();
