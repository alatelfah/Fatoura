import { Select } from 'antd';
import { useState } from 'react';
import { $api } from '../api/client';
import { Ltr } from './Ltr';

interface Props {
  value?: number;
  onChange?: (id: number) => void;
  kind?: 'clients' | 'suppliers';
  placeholder?: string;
  initialLabel?: string;
}

/** Searchable client (or supplier) picker backed by the paged list endpoint. */
export function ContactSelect({ value, onChange, kind = 'clients', placeholder, initialLabel }: Props) {
  const [search, setSearch] = useState('');
  const query = $api.useQuery('get', kind === 'clients' ? '/api/clients' : '/api/suppliers', {
    params: { query: { search: search || undefined, pageSize: 50 } },
  });
  const options = (query.data?.items ?? []).map((c) => ({
    value: c.id,
    label: c.name,
    trn: c.trn,
  }));
  if (value && initialLabel && !options.some((o) => o.value === value)) {
    options.unshift({ value, label: initialLabel, trn: '' });
  }

  return (
    <Select
      showSearch={{ filterOption: false, onSearch: setSearch }}
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      loading={query.isLoading}
      options={options}
      optionRender={(o) => (
        <div>
          {o.data.label}
          {o.data.trn && (
            <div style={{ fontSize: 12, opacity: 0.6 }}>
              TRN <Ltr>{o.data.trn}</Ltr>
            </div>
          )}
        </div>
      )}
      style={{ width: '100%' }}
      data-testid={`${kind}-select`}
    />
  );
}
