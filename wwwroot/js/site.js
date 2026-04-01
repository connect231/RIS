// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Detay kartlarını filtreye göre güncelle
window.updateDetayCards = async function(filter) {
	// Skeleton/Loading
	["faturalarTutar","faturalarAdet","tahsilatlarTutar","tahsilatlarAdet","sozlesmelerTutar","sozlesmelerAdet"].forEach(id => {
		var el = document.getElementById(id);
		if(el) el.innerHTML = '<span class="shimmer h-5 w-16 inline-block"></span>';
	});
	let params = '';
	const now = new Date();
	const y = now.getFullYear();
	const m = now.getMonth() + 1;
	switch(filter) {
		case 'lastmonth': {
			const lm = m === 1 ? 12 : m - 1;
			const ly = m === 1 ? y - 1 : y;
			const ld = new Date(ly, lm, 0).getDate();
			params = `startDate=${ly}-${String(lm).padStart(2,'0')}-01&endDate=${ly}-${String(lm).padStart(2,'0')}-${ld}`;
			break;
		}
		case 'q1': params = `startDate=${y}-01-01&endDate=${y}-03-31`; break;
		case 'q2': params = `startDate=${y}-04-01&endDate=${y}-06-30`; break;
		case 'q3': params = `startDate=${y}-07-01&endDate=${y}-09-30`; break;
		case 'q4': params = `startDate=${y}-10-01&endDate=${y}-12-31`; break;
		case 'ytd': params = `filter=ytd`; break;
		default: params = `startDate=${y}-${String(m).padStart(2,'0')}-01&endDate=${y}-${String(m).padStart(2,'0')}-${new Date(y, m, 0).getDate()}`;
	}
	try {
		const res = await fetch(`/Cockpit/GetDetayCardStats?${params}`);
		if(!res.ok) throw 0;
		const data = await res.json();
		console.log('Detay kart verisi:', data);
		// Faturalar
		var faturalarTutar = document.getElementById('faturalarTutar');
		if(faturalarTutar) faturalarTutar.textContent = '₺' + (data.faturalarToplam || 0).toLocaleString('tr-TR');
		var faturalarAdet = document.getElementById('faturalarAdet');
		if(faturalarAdet) faturalarAdet.textContent = (data.faturalarAdet || 0) + ' adet';
		// Tahsilatlar
		var tahsilatlarTutar = document.getElementById('tahsilatlarTutar');
		if(tahsilatlarTutar) tahsilatlarTutar.textContent = '₺' + (data.tahsilatlarToplam || 0).toLocaleString('tr-TR');
		var tahsilatlarAdet = document.getElementById('tahsilatlarAdet');
		if(tahsilatlarAdet) tahsilatlarAdet.textContent = (data.tahsilatlarAdet || 0) + ' adet';
		// Sözleşmeler
		var sozlesmelerTutar = document.getElementById('sozlesmelerTutar');
		if(sozlesmelerTutar) sozlesmelerTutar.textContent = '₺' + (data.sozlesmelerToplam || 0).toLocaleString('tr-TR');
		var sozlesmelerAdet = document.getElementById('sozlesmelerAdet');
		if(sozlesmelerAdet) sozlesmelerAdet.textContent = (data.sozlesmelerAdet || 0) + ' adet';
	} catch(e) {
		console.error('Detay kartları güncellenemedi', e);
		["faturalarTutar","faturalarAdet","tahsilatlarTutar","tahsilatlarAdet","sozlesmelerTutar","sozlesmelerAdet"].forEach(id => {
			var el = document.getElementById(id);
			if(el) el.textContent = 'Hata';
		});
	}
}
